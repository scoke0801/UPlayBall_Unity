$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Add-Type -AssemblyName System.Drawing
# 크로마키를 제거한 원화에서 연결된 세 전경만 분리한다. 색상이나 형태는 바꾸지 않는다.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
public static class ClubTrophyExporter {
    public static void Export(string source, string directory) {
        using (var bitmap = new Bitmap(source)) {
            int width = bitmap.Width, height = bitmap.Height;
            var visited = new bool[width * height];
            var components = new List<List<int>>();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                int start = y * width + x;
                if (visited[start] || bitmap.GetPixel(x, y).A == 0) continue;
                var pixels = new List<int>(); var queue = new Queue<int>();
                visited[start] = true; queue.Enqueue(start);
                while (queue.Count > 0) {
                    int point = queue.Dequeue(); pixels.Add(point);
                    int px = point % width, py = point / width;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
                        int nx = px + dx, ny = py + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int next = ny * width + nx;
                        if (visited[next] || bitmap.GetPixel(nx, ny).A == 0) continue;
                        visited[next] = true; queue.Enqueue(next);
                    }
                }
                if (pixels.Count > 1000) components.Add(pixels);
            }
            if (components.Count != 3) throw new InvalidOperationException("Expected three isolated trophy objects.");
            components.Sort((a,b) => MinX(a,width).CompareTo(MinX(b,width)));
            string[] names = { "pennant", "champion", "runner-up" };
            for (int i = 0; i < 3; i++) {
                var pixels = components[i]; int minX = width, minY = height, maxX = 0, maxY = 0;
                foreach (int point in pixels) { int x=point%width,y=point/width; minX=Math.Min(x,minX);maxX=Math.Max(x,maxX);minY=Math.Min(y,minY);maxY=Math.Max(y,maxY); }
                using (var output = new Bitmap(maxX-minX+33,maxY-minY+33,PixelFormat.Format32bppArgb)) {
                    foreach (int point in pixels) output.SetPixel(point%width-minX+16,point/width-minY+16,bitmap.GetPixel(point%width,point/width));
                    output.Save(System.IO.Path.Combine(directory,names[i]+".png"),ImageFormat.Png);
                }
            }
        }
    }
    static int MinX(List<int> pixels,int width) { int min=width; foreach(int point in pixels) min=Math.Min(min,point%width);return min; }
}
'@
[ClubTrophyExporter]::Export("$repo/Assets/10.Datas/Resources/UI/ClubHistory/trophies.png", "$repo/Assets/10.Datas/Resources/UI/ClubHistory")
