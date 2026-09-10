$uri = 'https://localhost:7263'
$cookieFile = 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\cookies.txt'

function Get-Token($html) {
    if ($html -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') {
        return $Matches[1]
    }
    if ($html -match 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') {
        return $Matches[1]
    }
    return $null
}

# 1. Login page
& curl.exe -s -k -c "$cookieFile" -b "$cookieFile" "$uri/Account/Login" -o 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\login.html'
$html = Get-Content 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\login.html' -Raw
$token = Get-Token $html
Write-Host "Login token: $token"
if (-not $token) { exit 1 }

# 2. Login POST
& curl.exe -s -k -c "$cookieFile" -b "$cookieFile" -L "$uri/Account/Login" `
    -d "__RequestVerificationToken=$token" `
    -d "Email=sattvikoramdhani@gmail.com" `
    -d "Password=MasterAdmin2024!" `
    -d "RememberMe=false" -o 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\afterlogin.html'
Write-Host "Login posted"

# 3. Get User Management partial
& curl.exe -s -k -c "$cookieFile" -b "$cookieFile" "$uri/UserManagement/Index" -o 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\um.html'
$umHtml = Get-Content 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\um.html' -Raw
$deleteToken = Get-Token $umHtml
Write-Host "Delete token: $deleteToken"

# 4. Find first user id from a hidden id input
$idMatch = [regex]::Match($umHtml, '<input[^>]*name="id"[^>]*value="(\d+)"')
Write-Host "Id match: $($idMatch.Groups[1].Value)"
if (-not $idMatch.Success) { exit 1 }
$targetId = $idMatch.Groups[1].Value
Write-Host "Deleting user id $targetId"

# 5. Delete POST
& curl.exe -s -k -c "$cookieFile" -b "$cookieFile" "$uri/UserManagement/Delete" `
    -d "__RequestVerificationToken=$deleteToken" `
    -d "id=$targetId" -o 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\delresult.json'
$result = Get-Content 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\delresult.json' -Raw
Write-Host "Delete result: $result"
