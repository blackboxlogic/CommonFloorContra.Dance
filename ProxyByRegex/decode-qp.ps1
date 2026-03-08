# if a "raw" email is copied out of gmail, this will fix the escaping.
$file = 'C:\Users\ahennings\Documents\cfcd\ProxyByRegex\SampleEmail.html'
$content = [System.IO.File]::ReadAllText($file)

# Remove soft line breaks (= at end of line)
$content = $content -replace '=\r?\n', ''

# Decode consecutive =XX hex sequences as UTF-8 bytes
$content = [System.Text.RegularExpressions.Regex]::Replace($content, '((?:=[0-9A-Fa-f]{2})+)', {
    param($m)
    $bytes = ($m.Value -split '=' | Where-Object { $_ }) | ForEach-Object { [Convert]::ToByte($_, 16) }
    [System.Text.Encoding]::UTF8.GetString([byte[]]$bytes)
})

[System.IO.File]::WriteAllText($file, $content, [System.Text.Encoding]::UTF8)
Write-Host 'Done'
