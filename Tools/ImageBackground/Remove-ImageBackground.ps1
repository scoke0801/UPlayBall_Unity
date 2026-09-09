param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [ValidateSet('Neutral','ChromaKey')][string]$Mode = 'Neutral',
    [ValidatePattern('^#[0-9a-fA-F]{6}$')][string]$KeyColor = '#FF00FF',
    [ValidateRange(0,254)][int]$KeyTolerance = 24,
    [ValidateRange(1,255)][int]$KeyOpaqueDistance = 180,
    [ValidateRange(0,16)][int]$KeyEdgeRadius = 6,
    [ValidateRange(0,255)][int]$MaxChroma = 12,
    [ValidateRange(0,255)][int]$MinBrightness = 105,
    [ValidateRange(0,255)][int]$MaxBrightness = 255,
    [ValidateRange(0,8)][int]$EdgeMatteRadius = 0,
    [ValidateRange(0,255)][int]$ProtectBrightThreshold = 0,
    [string]$ProtectPolygonPath,
    [switch]$RemoveEnclosedBackground
)
# 무채색 단색·체크무늬 배경을 제거한다. 내부 영역 제거는 보호 다각형과 함께 명시적으로 선택한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if ($Mode -eq 'ChromaKey') {
    if ($KeyTolerance -ge $KeyOpaqueDistance) { throw 'KeyTolerance는 KeyOpaqueDistance보다 작아야 합니다.' }
    if ($ProtectPolygonPath -or $RemoveEnclosedBackground -or $ProtectBrightThreshold -gt 0 -or $EdgeMatteRadius -gt 0) {
        throw '크로마키 모드에는 무채색 배경용 보호·경계 옵션을 함께 지정하지 않습니다.'
    }
    $sourcePath = (Resolve-Path -LiteralPath $InputPath).Path
    $targetPath = [IO.Path]::GetFullPath($OutputPath)
    if (Test-Path -LiteralPath $targetPath) { throw '출력 파일이 이미 있습니다. 다른 이름을 지정하세요.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath)) | Out-Null
    Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'ChromaKeyRemoval.cs')
    [Baseball.Tools.ImageBackground.ChromaKeyRemoval]::Run($sourcePath, $targetPath, [Drawing.ColorTranslator]::FromHtml($KeyColor), $KeyTolerance, $KeyOpaqueDistance, $KeyEdgeRadius)
    return
}
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Baseball.Tools.ImageBackground {
    /// <summary>전경 보호 다각형을 보존하며 저채도 배경을 제거하는 로컬 도구다.</summary>
    public static class BackgroundRemoval {
        public static void Run(string input, string output, int chroma, int brightness, int maxBrightness, int edgeRadius, int protectBrightThreshold, Point[][] polygons, bool removeEnclosed) {
            using (var source = new Bitmap(input))
            using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb)) {
                using (var graphics = Graphics.FromImage(bitmap)) {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(source, 0, 0);
                }
                int w = bitmap.Width, h = bitmap.Height;
                var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                var bytes = new byte[data.Stride * h];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                var removed = new bool[w * h];
                var protectedPixels = new bool[w*h];
                if(polygons.Length > 0) {
                    using(var mask = new Bitmap(w,h)) {
                        using(var g = Graphics.FromImage(mask)) {
                            foreach(var polygon in polygons) g.FillPolygon(Brushes.White,polygon);
                        }
                        for(int y=0;y<h;y++) for(int x=0;x<w;x++) protectedPixels[y*w+x]=mask.GetPixel(x,y).R>0;
                    }
                }
                var queue = new Queue<int>();
                // 밝은 의상은 보호 영역에서 이어진 하이라이트까지 보존해 손으로 그린 다각형 경계가 드러나지 않게 한다.
                if(protectBrightThreshold > 0) {
                    for(int i=0;i<protectedPixels.Length;i++) if(protectedPixels[i]) queue.Enqueue(i);
                    Action<int,int> protectBright = (x,y) => {
                        if(x<0 || x>=w || y<0 || y>=h || protectedPixels[y*w+x]) return;
                        int p=y*data.Stride+x*4;
                        int low=Math.Min(bytes[p],Math.Min(bytes[p+1],bytes[p+2]));
                        int high=Math.Max(bytes[p],Math.Max(bytes[p+1],bytes[p+2]));
                        if(bytes[p+3]==0 || high<=protectBrightThreshold || low<protectBrightThreshold-15) return;
                        protectedPixels[y*w+x]=true;
                        queue.Enqueue(y*w+x);
                    };
                    while(queue.Count>0) {
                        int index=queue.Dequeue(), x=index%w,y=index/w;
                        protectBright(x-1,y); protectBright(x+1,y); protectBright(x,y-1); protectBright(x,y+1);
                    }
                }
                Action<int,int> visit = (x,y) => {
                    if (x < 0 || y < 0 || x >= w || y >= h) return;
                    int index = y*w+x, p = y*data.Stride+x*4;
                    if (removed[index] || protectedPixels[index]) return;
                    int min = Math.Min(bytes[p], Math.Min(bytes[p+1], bytes[p+2]));
                    int max = Math.Max(bytes[p], Math.Max(bytes[p+1], bytes[p+2]));
                    if (bytes[p+3] != 0 && (max-min > chroma || min < brightness || max > maxBrightness)) return;
                    removed[index] = true;
                    queue.Enqueue(index);
                };
                for (int x=0;x<w;x++) { visit(x,0); visit(x,h-1); }
                for (int y=0;y<h;y++) { visit(0,y); visit(w-1,y); }
                // 머리카락 사이처럼 닫힌 영역도 제거할 때는 흰 의상과 눈을 명시적으로 보호한다.
                if(removeEnclosed) for(int y=0;y<h;y++) for(int x=0;x<w;x++) visit(x,y);
                while(queue.Count>0) {
                    int index=queue.Dequeue(), x=index%w, y=index/w;
                    visit(x-1,y); visit(x+1,y); visit(x,y-1); visit(x,y+1);
                }
                // 캐릭터와 연결되지 않은 체크무늬 잔여 조각은 가장 큰 전경 성분 밖으로 제거한다.
                var visited = new bool[w*h];
                var largest = new List<int>();
                var protectedComponents = new List<int>();
                for(int start=0;start<w*h;start++) {
                    if(removed[start] || visited[start] || bytes[(start/w)*data.Stride+(start%w)*4+3]==0) continue;
                    var component = new List<int>();
                    bool hasProtection = false;
                    queue.Enqueue(start); visited[start]=true;
                    while(queue.Count>0) {
                        int p=queue.Dequeue(); component.Add(p);
                        hasProtection |= protectedPixels[p];
                        int x=p%w,y=p/w;
                        int[] neighbors={x>0?p-1:-1,x<w-1?p+1:-1,y>0?p-w:-1,y<h-1?p+w:-1};
                        foreach(int n in neighbors) if(n>=0 && !removed[n] && !visited[n] && bytes[(n/w)*data.Stride+(n%w)*4+3]!=0) { visited[n]=true; queue.Enqueue(n); }
                    }
                    if(component.Count>largest.Count) largest=component;
                    if(hasProtection) protectedComponents.AddRange(component);
                }
                // 떨어진 소품도 보호 영역을 포함하면 연결 성분 전체를 보존한다.
                for(int p=0;p<removed.Length;p++) removed[p]=true;
                foreach(int p in largest) removed[p]=false;
                foreach(int p in protectedComponents) removed[p]=false;
                if(edgeRadius > 0) MatteEdges(bytes, data.Stride, w, h, removed, protectedPixels, edgeRadius);
                int count=0;
                for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
                    int p=y*data.Stride+x*4;
                    if(removed[y*w+x]) { bytes[p]=bytes[p+1]=bytes[p+2]=bytes[p+3]=0; count++; }
                }
                Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
                bitmap.UnlockBits(data);
                if(count==0 || count==w*h) throw new InvalidOperationException("배경 추출 결과가 유효하지 않습니다.");
                bitmap.Save(output,ImageFormat.Png);
                Console.WriteLine("RGBA PNG: {0}x{1}; transparent pixels: {2}/{3}",w,h,count,w*h);
            }
        }

        // 무채색 배경이 섞인 저채도 경계만 주변의 유채색 전경을 기준으로 복원한다.
        private static void MatteEdges(byte[] bytes, int stride, int w, int h, bool[] removed, bool[] protection, int radius) {
            var original = (byte[])bytes.Clone();
            int search = radius + 3;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
                int index=y*w+x, p=y*stride+x*4;
                if(removed[index] || protection[index]) continue;
                int low=Math.Min(original[p],Math.Min(original[p+1],original[p+2]));
                int high=Math.Max(original[p],Math.Max(original[p+1],original[p+2]));
                int colorRange=high-low;
                if(low<85 || colorRange>=40) continue;
                int background=-1, backgroundDistance=int.MaxValue;
                int foreground=-1, foregroundDistance=int.MaxValue;
                for(int dy=-search;dy<=search;dy++) for(int dx=-search;dx<=search;dx++) {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0 || nx>=w || ny<0 || ny>=h) continue;
                    int q=ny*stride+nx*4, distance=dx*dx+dy*dy;
                    if(removed[ny*w+nx]) {
                        if(distance<=radius*radius && distance<backgroundDistance) { background=q; backgroundDistance=distance; }
                    } else {
                        int min=Math.Min(original[q],Math.Min(original[q+1],original[q+2]));
                        int max=Math.Max(original[q],Math.Max(original[q+1],original[q+2]));
                        if(max-min>=45 && distance<foregroundDistance) { foreground=q; foregroundDistance=distance; }
                    }
                }
                if(background<0 || foreground<0) continue;
                int fmin=Math.Min(original[foreground],Math.Min(original[foreground+1],original[foreground+2]));
                int fmax=Math.Max(original[foreground],Math.Max(original[foreground+1],original[foreground+2]));
                double alpha=Math.Min(1.0,(double)colorRange/(fmax-fmin));
                // 원본 알파를 올리지 않고 배경색을 역합성해 회색 테두리를 줄인다.
                for(int channel=0;channel<3;channel++) {
                    double recovered=(original[p+channel]-(1-alpha)*original[background+channel])/Math.Max(alpha,0.01);
                    bytes[p+channel]=(byte)Math.Max(0,Math.Min(255,Math.Round(recovered)));
                }
                bytes[p+3]=(byte)Math.Round(original[p+3]*alpha);
            }
        }
    }
}
'@
$sourcePath = (Resolve-Path -LiteralPath $InputPath).Path
$targetPath = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $targetPath) { throw '출력 파일이 이미 있습니다. 다른 이름을 지정하세요.' }
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($targetPath)) | Out-Null
$polygons = [System.Collections.Generic.List[System.Drawing.Point[]]]::new()
if ($ProtectPolygonPath) {
    $points = Get-Content -LiteralPath $ProtectPolygonPath -Raw | ConvertFrom-Json
    if ($points.Count -lt 1) { throw '보호 다각형이 비어 있습니다.' }
    # 기존 단일 다각형과 얼굴·의상 등 여러 다각형의 배열을 모두 지원한다.
    if ($points[0][0] -is [System.Array]) { $polygonGroups = $points }
    else { $polygonGroups = @(); $polygonGroups += ,$points }
    foreach ($group in $polygonGroups) {
        if ($group.Count -lt 3) { throw '보호 다각형에는 최소 세 점이 필요합니다.' }
        $polygon = @($group | ForEach-Object {
            if ($_.Count -ne 2) { throw '보호 좌표는 [x,y] 형식이어야 합니다.' }
            [System.Drawing.Point]::new([int]$_[0], [int]$_[1])
        })
        $polygons.Add([System.Drawing.Point[]]$polygon)
    }
}
if ($RemoveEnclosedBackground -and $polygons.Count -eq 0) {
    throw '내부 배경 제거에는 얼굴·흰 의상을 보존할 ProtectPolygonPath가 필요합니다.'
}
if ($MinBrightness -gt $MaxBrightness) { throw 'MinBrightness는 MaxBrightness 이하여야 합니다.' }
if ($ProtectBrightThreshold -gt 0 -and $polygons.Count -eq 0) { throw '밝은 전경 확장에는 ProtectPolygonPath가 필요합니다.' }
[Baseball.Tools.ImageBackground.BackgroundRemoval]::Run($sourcePath, $targetPath, $MaxChroma, $MinBrightness, $MaxBrightness, $EdgeMatteRadius, $ProtectBrightThreshold, $polygons.ToArray(), $RemoveEnclosedBackground.IsPresent)
