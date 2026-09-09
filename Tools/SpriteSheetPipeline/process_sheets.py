"""원본을 보존하며 크로마키 제거, 셀 분할, 검수 산출물을 재현한다."""
from __future__ import annotations
import argparse, colorsys, hashlib, json, math, os, statistics, subprocess, sys, tempfile
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'output/sprite-sheet-ingame/python-deps'))
from PIL import Image, ImageDraw
VERSION = '1.0.0'

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def write_json(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def boundaries(size, count):
    return [round(i * size / count) for i in range(count + 1)]

def validate_sheet(sheet):
    if any(type(sheet[key]) is not int or sheet[key] <= 0 for key in ('rows', 'cols')):
        raise ValueError('행과 열은 양의 정수여야 함')
    if sheet['reviewStatus'] not in ('Approved', 'NeedsReview') or sheet['handedness'] not in ('R', 'L', 'Shared', 'NeedsReview'):
        raise ValueError('알 수 없는 검수 상태 또는 손잡이')
    count = sheet['rows'] * sheet['cols']
    order, excluded = sheet['frameOrder'], sheet['excludedFrames']
    if any(type(cell) is not int for cell in order + excluded) or len(set(excluded)) != len(excluded):
        raise ValueError('셀 번호는 중복 없는 정수여야 함')
    if not order or len(set(order)) != len(order) or set(order) & set(excluded):
        raise ValueError('프레임 순서 중복/제외 충돌')
    if set(order) | set(excluded) != set(range(count)):
        raise ValueError('모든 셀은 순서 또는 제외 목록에 있어야 함')
    for cell, pivot in sheet.get('framePivots', {}).items():
        if cell not in {str(index) for index in order} or any(
                not math.isfinite(pivot[key]) or not 0 <= pivot[key] <= 1 for key in ('x', 'groundY')):
            raise ValueError('프레임 접지 좌표는 재생 셀 내부의 유한한 정규화 좌표여야 함')
    durations = sheet['durationsMs']
    if len(durations) != len(order) or any(not math.isfinite(value) or value <= 0 for value in durations):
        raise ValueError('재생 프레임마다 유한한 양수 체류 시간이 필요함')
    events = sheet['events']
    allowed_events = {'BallRelease', 'SwingWindowOpen', 'BatContact', 'GloveContact', 'Transfer',
                      'ThrowRelease', 'ThrowReady', 'BatDrop', 'RunStart', 'Miss', 'Reaction'}
    names = [event['name'] for event in events]
    if len(set(names)) != len(names) or any(name not in allowed_events for name in names):
        raise ValueError('알 수 없거나 중복된 사건')
    for event in events:
        if type(event['frameIndex']) is not int or not 0 <= event['frameIndex'] < len(order):
            raise ValueError('이벤트 프레임 범위 오류')
    indexes = {e['name']: e['frameIndex'] for e in events}
    for before, after in [('SwingWindowOpen','BatContact'),('GloveContact','Transfer'),('GloveContact','ThrowRelease'),('Transfer','ThrowRelease'),('GloveContact','ThrowReady'),('BatDrop','RunStart')]:
        if before in indexes and after in indexes and indexes[before] > indexes[after]:
            raise ValueError('이벤트 인과 순서 오류')
    if sheet['reviewStatus'] == 'Approved' and sheet['handedness'] == 'NeedsReview':
        raise ValueError('미확정 손잡이 Production 금지')
    for mask in sheet['objectMasks']:
        if mask['cellIndex'] not in order or len(mask['rect']) != 4 or min(mask['rect']) < 0:
            raise ValueError('공 제거 mask 오류')

def estimate_key(image, fraction):
    w,h=image.size; band=max(1,round(min(w,h)*fraction))
    pixels=image.load(); samples=[]
    for y in range(0,h,3):
        for x in range(0,w,3):
            if x<band or y<band or x>=w-band or y>=h-band:
                samples.append(pixels[x,y][:3])
    return tuple(round(statistics.median(p[c] for p in samples)) for c in range(3))

def green_candidate(rgb, key, config):
    r,g,b=rgb; hue,saturation,value=colorsys.rgb_to_hsv(r/255,g/255,b/255)
    distance=math.sqrt(sum((a-b)**2 for a,b in zip(rgb,key)))
    return config['hueMin'] <= hue <= config['hueMax'] and saturation >= config['saturationMin'] and value >= config['valueMin'] and g-max(r,b)>=config['dominanceMin'] and distance<=config['coreDistance']

def remove_background(source, target, key, config):
    # 저장소 정본 제거 도구의 soft matte/edge 복원을 재사용한다.
    command=['powershell','-NoProfile','-File',str(ROOT/'Tools/ImageBackground/Remove-ImageBackground.ps1'),'-InputPath',str(source),'-OutputPath',str(target),'-Mode','ChromaKey','-KeyColor','#%02X%02X%02X'%key,'-KeyTolerance',str(config['keyTolerance']),'-KeyOpaqueDistance',str(config['keyOpaqueDistance']),'-KeyEdgeRadius',str(config['keyEdgeRadius'])]
    subprocess.run(command,check=True,capture_output=True)
    original=Image.open(source).convert('RGBA'); result=Image.open(target).convert('RGBA')
    src=list(original.getdata()); data=list(result.getdata()); changed=[]
    for rgb,rgba in zip(src,data):
        r,g,b,a=rgba
        if green_candidate(rgb[:3],key,config): a=0
        if 0<a<255 and g>max(r,b)+config['spillTolerance']:
            g=max(r,b)+config['spillTolerance']
        changed.append((r,g,b,a))
    result.putdata(changed); result.save(target)
    return result

def process_sheet(sheet, manifest, output):
    validate_sheet(sheet)
    source=(ROOT/sheet['sourceFile']).resolve()
    if not source.is_relative_to(ROOT): raise ValueError('원본은 저장소 내부 경로여야 함')
    source_hash=digest(source)
    if source_hash!=sheet['sourceHash']: raise ValueError('원본 hash 변경: 명시적 재감사 필요 '+sheet['sheetId'])
    config=manifest['chroma']; build_hash=hashlib.sha256((source_hash+json.dumps(sheet,sort_keys=True)+json.dumps(config,sort_keys=True)+json.dumps(manifest['importSettings'],sort_keys=True)+digest(__file__)+digest(ROOT/'Tools/ImageBackground/ChromaKeyRemoval.cs')+digest(ROOT/'Tools/ImageBackground/Remove-ImageBackground.ps1')).encode()).hexdigest()
    folder=output/'Processed'/sheet['sheetId']; folder.mkdir(parents=True,exist_ok=True)
    metadata_path=folder/(sheet['sheetId']+'.json'); cache=folder/'build.json'
    if cache.exists():
        record=json.loads(cache.read_text(encoding='utf-8'))
        if record['buildHash']==build_hash and all((folder/f).exists() and digest(folder/f)==h for f,h in record['files'].items()):
            return json.loads(metadata_path.read_text(encoding='utf-8')),True
    original=Image.open(source).convert('RGBA'); key=estimate_key(original,config['borderFraction'])
    with tempfile.TemporaryDirectory(prefix='sprite-key-') as temp:
        image=remove_background(source,Path(temp)/'rgba.png',key,config)
    w,h=image.size; xs=boundaries(w,sheet['cols']); ys=boundaries(h,sheet['rows']); frames=[]; preview=[]
    max_w=max(b-a for a,b in zip(xs,xs[1:])); max_h=max(b-a for a,b in zip(ys,ys[1:])); atlas=Image.new('RGBA',(max_w*sheet['cols'],max_h*math.ceil(len(sheet['frameOrder'])/sheet['cols'])))
    contact=Image.new('RGB',(max_w*sheet['cols'],(max_h+24)*math.ceil(len(sheet['frameOrder'])/sheet['cols'])),(28,31,42)); draw=ImageDraw.Draw(contact)
    foreground_counts=[]; baselines=[]
    for index,cell in enumerate(sheet['frameOrder']):
        col,row=cell%sheet['cols'],cell//sheet['cols']; x,y=xs[col],ys[row]; cw,ch=xs[col+1]-x,ys[row+1]-y
        frame=image.crop((x,y,x+cw,y+ch)); masks=[]
        for mask in sheet['objectMasks']:
            if mask['cellIndex']==cell:
                mx,my,mw,mh=mask['rect']
                if mx+mw>cw or my+mh>ch: raise ValueError('mask 셀 범위 초과')
                frame.paste((0,0,0,0),(mx,my,mx+mw,my+mh)); masks.append(mask)
        bbox=frame.getchannel('A').getbbox()
        if bbox is None: raise ValueError('빈 프레임 '+str(cell))
        l,t,r,b=bbox; baselines.append(b/ch); foreground_counts.append(sum(a>0 for a in frame.getchannel('A').getdata()))
        filename=f'frame_{index:03}.png'; frame.save(folder/filename)
        pivot=sheet.get('framePivots', {}).get(str(cell), sheet['pivot'])
        duration=sheet['durationsMs'][index]; events=[dict(e, hasSourcePosition=e.get('sourcePositionNormalized') is not None) for e in sheet['events'] if e['frameIndex']==index]
        frames.append({'file':filename,'cellIndex':cell,'durationMs':duration,
            'sourceCellRect':{'x':x,'y':y,'width':cw,'height':ch},
            'trimRect':{'x':l,'y':t,'width':r-l,'height':b-t},
            'pivot':{'x':pivot['x'],'y':1-pivot['groundY']},
            'pivotNormalized':{'x':pivot['x'],'y':1-pivot['groundY']},
            'rootOffsetPx':{'x':cw*pivot['x']-l,'y':ch*pivot['groundY']-t},
            'groundBaselinePx':ch*pivot['groundY'],'events':events,'objectMasks':masks})
        ax=(index%sheet['cols'])*max_w; ay=(index//sheet['cols'])*max_h; atlas.paste(frame,(ax,ay))
        cy=(index//sheet['cols'])*(max_h+24); bg=(238,238,238) if index%2 else (28,31,42); tile=Image.new('RGB',(max_w,max_h),bg);tile.paste(frame,(0,0),frame);contact.paste(tile,(ax,cy));draw.text((ax+6,cy+max_h+3),f'{index}: cell {cell} / {duration}ms',fill='white')
        animation=Image.new('RGB',(max_w,max_h),(50,55,65));animation.paste(frame,(0,0),frame); animation.thumbnail((256,256)); preview.append(animation)
    atlas.save(folder/(sheet['sheetId']+'.atlas.png'));contact.save(folder/(sheet['sheetId']+'_contact.png'))
    preview[0].save(folder/(sheet['sheetId']+'_preview.gif'),save_all=True,append_images=preview[1:],duration=sheet['durationsMs'],loop=0,disposal=2)
    image.getchannel('A').save(folder/'alpha.png')
    heat=Image.new('RGB',image.size); heat.putdata([(max(0,g-max(r,b))*4 if 0<a<255 else 0,0,0) for r,g,b,a in image.getdata()]);heat.save(folder/'spill.png')
    data=list(image.getdata()); original_data=list(original.getdata()); background=[p[3] for p,s in zip(data,original_data) if green_candidate(s[:3],key,config)]; edges=[max(0,g-max(r,b)) for r,g,b,a in data if 0<a<255]
    qa={'clipId':sheet['sheetId'],'keyMedian':key,'sourceSize':[w,h],'foregroundPixelsPerFrame':foreground_counts,'backgroundAlphaMean':sum(background)/max(1,len(background)),'edgeGreenDominanceMax':max(edges,default=0),'groundBoundsDriftNormalized':max(baselines)-min(baselines),'frameCount':len(frames),'needsReview':sheet['reviewStatus']!='Approved','warnings':sheet.get('reviewNotes',[])}
    write_json(folder/'qa_report.json',qa)
    metadata={k:sheet[k] for k in ['sheetId','sourceFile','rows','cols','handedness','reviewStatus','frameOrder','excludedFrames','loop']}
    # Unity JsonUtility는 생략된 중첩 객체를 기본값으로 복원하므로 좌표 존재 여부를 별도로 전달한다.
    metadata['events']=[dict(e, hasSourcePosition=e.get('sourcePositionNormalized') is not None) for e in sheet['events']]
    metadata.update({'schemaVersion':2,'sourceHash':source_hash,'buildHash':build_hash,'frames':frames,'importSettings':manifest['importSettings']});write_json(metadata_path,metadata)
    files={p.name:digest(p) for p in sorted(folder.iterdir()) if p.is_file() and p.name!='build.json'}
    write_json(cache,{'buildHash':build_hash,'files':files})
    return metadata,False

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--manifest',type=Path,default=ROOT/'Tools/SpriteSheetPipeline/sprite_sheet_sources.json');parser.add_argument('--output',type=Path,default=ROOT/'output/sprite-sheet-ingame');parser.add_argument('--validate-only',action='store_true');args=parser.parse_args()
    manifest=json.loads(args.manifest.read_text(encoding='utf-8-sig')); output=args.output.resolve()
    if not output.is_relative_to(ROOT/'output'): raise ValueError('출력은 저장소 output 하위에만 생성')
    clips=[]; summary=[]
    for sheet in manifest['sheets']:
        validate_sheet(sheet)
        if args.validate_only: continue
        meta,cached=process_sheet(sheet,manifest,output);clips.append({'metadataFile':f'Processed/{sheet["sheetId"]}/{sheet["sheetId"]}.json'});summary.append({'clipId':sheet['sheetId'],'cached':cached,'reviewStatus':meta['reviewStatus']});print(sheet['sheetId'], 'cached' if cached else 'built',flush=True)
    if not args.validate_only:
        write_json(output/'index.json',{'schemaVersion':1,'clips':clips});write_json(output/'run_report.json',summary)

if __name__=='__main__': main()
