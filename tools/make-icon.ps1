Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outPng = Join-Path $root 'docs\icon.png'
$outIco = Join-Path $root 'app.ico'
$size = 256

$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Fondo redondeado
$bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(18, 20, 26))
$bgPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 52
$bgPath.AddArc(0, 0, $r, $r, 180, 90)
$bgPath.AddArc($size - $r, 0, $r, $r, 270, 90)
$bgPath.AddArc($size - $r, $size - $r, $r, $r, 0, 90)
$bgPath.AddArc(0, $size - $r, $r, $r, 90, 90)
$bgPath.CloseFigure()
$g.FillPath($bgBrush, $bgPath)

$borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(42, 47, 58)), 3
$g.DrawPath($borderPen, $bgPath)

# Campana
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(64, 214, 128))
$g.FillEllipse($green, 68, 54, 120, 120)     # cúpula
$g.FillRectangle($green, 88, 114, 80, 54)    # cuerpo
$g.FillEllipse($green, 68, 148, 120, 42)     # base redondeada
$g.FillEllipse($green, 114, 184, 28, 24)     # badajo

# Asa superior
$handlePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(64, 214, 128)), 9
$g.DrawArc($handlePen, 112, 32, 32, 32, 180, 180)

# Ondas rojas de alerta
$redPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 77, 109)), 9
$redPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$redPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawArc($redPen, 190, 86, 40, 84, -58, 116)
$g.DrawArc($redPen, 198, 66, 48, 124, -55, 110)

$g.Dispose()
$bmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)

# ICO con PNG comprimido (256x256)
$png = [System.IO.File]::ReadAllBytes($outPng)
$fs = [System.IO.File]::Create($outIco)
$w = New-Object System.IO.BinaryWriter($fs)
$w.Write([UInt16]0)
$w.Write([UInt16]1)
$w.Write([UInt16]1)
$w.Write([Byte]0)
$w.Write([Byte]0)
$w.Write([Byte]0)
$w.Write([Byte]0)
$w.Write([UInt16]1)
$w.Write([UInt16]32)
$w.Write([UInt32]$png.Length)
$w.Write([UInt32]22)
$w.Write($png)
$w.Flush()
$w.Close()
$bmp.Dispose()

Write-Host "Icono generado: $outIco"
