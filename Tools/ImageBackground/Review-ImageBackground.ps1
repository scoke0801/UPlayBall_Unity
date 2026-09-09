param(
    [Parameter(Mandatory=$true)][string[]]$InputPaths,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
# 실제 알파 통계와 밝고 어두운 배경 합성을 같은 납품 파일에서 만든다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Baseball.Tools.ImageBackground {
    public sealed class AlphaReport {
        public string Path;
        public int Width, Height, Transparent, Partial, Opaque;
        public bool TransparentCorners;
    }
    public static class AlphaReview {
        /// <summary>납품 PNG의 알파 값을 측정하고 두 배경에 합성해 검수한다.</summary>
        public static AlphaReport[] Run(string[] paths, string output) {
            var reports = new AlphaReport[paths.Length];
            using(var sheet = new Bitmap(paths.Length*256, 816))
            using(var g = Graphics.FromImage(sheet))
            using(var font = new Font(FontFamily.GenericSansSerif, 12)) {
                g.Clear(Color.FromArgb(244,241,235));
                g.FillRectangle(Brushes.DarkSlateGray,0,408,sheet.Width,408);
                for(int i=0;i<paths.Length;i++) using(var source = new Bitmap(paths[i])) {
                    if(source.PixelFormat != PixelFormat.Format32bppArgb) throw new InvalidOperationException("RGBA PNG가 아닙니다: "+paths[i]);
                    var report = new AlphaReport { Path=paths[i], Width=source.Width, Height=source.Height };
                    var data=source.LockBits(new Rectangle(0,0,source.Width,source.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                    try {
                        var bytes=new byte[data.Stride*source.Height];
                        Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
                        for(int y=0;y<source.Height;y++) for(int x=0;x<source.Width;x++) {
                            byte alpha=bytes[y*data.Stride+x*4+3];
                            if(alpha==0) report.Transparent++; else if(alpha==255) report.Opaque++; else report.Partial++;
                        }
                    } finally {source.UnlockBits(data);}
                    report.TransparentCorners=source.GetPixel(0,0).A==0 && source.GetPixel(source.Width-1,0).A==0 && source.GetPixel(0,source.Height-1).A==0 && source.GetPixel(source.Width-1,source.Height-1).A==0;
                    if(!report.TransparentCorners || report.Transparent==0 || report.Opaque==0) throw new InvalidOperationException("알파 검증 실패: "+paths[i]);
                    reports[i]=report;
                    float scale=Math.Min(256f/source.Width,384f/source.Height);
                    for(int row=0;row<2;row++) {
                        g.DrawImage(source,i*256+(256-source.Width*scale)/2,row*408+24,source.Width*scale,source.Height*scale);
                        g.DrawString(System.IO.Path.GetFileNameWithoutExtension(paths[i]),font,row==0?Brushes.Black:Brushes.White,i*256,row*408);
                    }
                }
                sheet.Save(output,ImageFormat.Png);
            }
            return reports;
        }
    }
}
'@
$target=[IO.Path]::GetFullPath($OutputPath)
if((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath ($target + '.json'))) {throw '검수 파일이 이미 있습니다. 다른 이름을 지정하세요.'}
$paths=@($InputPaths | ForEach-Object {(Resolve-Path -LiteralPath $_).Path})
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
$reports=[Baseball.Tools.ImageBackground.AlphaReview]::Run($paths,$target)
$reports | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ($target + '.json') -Encoding UTF8
$reports | Format-Table Width,Height,Transparent,Partial,Opaque,TransparentCorners,Path
