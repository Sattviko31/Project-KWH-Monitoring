$uri = 'https://localhost:7263'
$cookieFile = 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\cookies2.txt'
$email = "testadmin_$(Get-Date -Format yyyyMMdd_HHmmss)@example.com"
$pass = 'AdminPass123!'

function Get-Token($html) {
    if ($html -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { return $Matches[1] }
    if ($html -match 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') { return $Matches[1] }
    return $null
}

function Invoke-Curl($url, $method='GET', $body=$null, $outFile) {
    if ($method -eq 'POST' -and $body) {
        $postData = $body -join '&'
        & curl.exe -s -k -c "$cookieFile" -b "$cookieFile" -X POST "$url" -d "$postData" -o "$outFile"
    } else {
        & curl.exe -s -k -c "$cookieFile" -b "$cookieFile" "$url" -o "$outFile"
    }
}

# Register
Invoke-Curl "$uri/Account/Register" -outFile 'reg2.html'
$regToken = Get-Token (Get-Content 'reg2.html' -Raw)
Invoke-Curl "$uri/Account/Register" -method POST -body @(
    "__RequestVerificationToken=$regToken",
    "Email=$email",
    "DisplayName=Test Admin",
    "Password=$pass",
    "ConfirmPassword=$pass"
) -outFile 'after_reg2.html'
Write-Host "Registered $email"

# Update via SQL: confirm email and set role Admin
$sql = "UPDATE ApplicationUsers SET EmailConfirmed = 1, Role = 'Admin' WHERE Email = '$email';"
$sql | Out-File 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\update_admin.sql' -Encoding ASCII
& sqlcmd -S 192.168.168.38,1433 -U kwhapp -P kwhapp1234 -d HaiwellElectrical -C -i 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\update_admin.sql'
Write-Host "Updated role/confirmed via SQL"

# Login as this admin
Invoke-Curl "$uri/Account/Login" -outFile 'login2.html'
$loginToken = Get-Token (Get-Content 'login2.html' -Raw)
Invoke-Curl "$uri/Account/Login" -method POST -body @(
    "__RequestVerificationToken=$loginToken",
    "Email=$email",
    "Password=$pass",
    "RememberMe=false"
) -outFile 'afterlogin2.html'
Write-Host "Logged in as $email"

# Fetch settings
Invoke-Curl "$uri/Monitoring/Settings" -outFile 'settings2.html'
$settingsHtml = Get-Content 'settings2.html' -Raw
if ($settingsHtml -match 'User Management') {
    Write-Host 'FAIL: User Management tab is visible for non-master admin'
} else {
    Write-Host 'PASS: User Management tab is hidden for non-master admin'
}

# Also try direct access to the partial
Invoke-Curl "$uri/UserManagement/Index" -outFile 'um2.html'
$umHtml = Get-Content 'um2.html' -Raw
if ($umHtml -match 'AccessDenied|Akses ditolak|access denied') {
    Write-Host 'PASS: Direct access to UserManagement/Index denied'
} else {
    Write-Host 'INFO: UserManagement/Index response length:' $umHtml.Length
}
