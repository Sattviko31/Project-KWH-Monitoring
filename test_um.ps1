$uri = 'https://localhost:7263'
$cookieFile = 'd:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\cookies.txt'

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

# Register a test user
$regEmail = "testuser_$(Get-Date -Format yyyyMMdd_HHmmss)@example.com"
$regPass = 'TestPass123!'

Invoke-Curl "$uri/Account/Register" -outFile 'register.html'
$regToken = Get-Token (Get-Content 'register.html' -Raw)
Write-Host "Register token: $regToken"
Invoke-Curl "$uri/Account/Register" -method POST -body @(
    "__RequestVerificationToken=$regToken",
    "Email=$regEmail",
    "DisplayName=Test User",
    "Password=$regPass",
    "ConfirmPassword=$regPass"
) -outFile 'after_register.html'
Write-Host "Registered $regEmail"

# Login as master
Invoke-Curl "$uri/Account/Login" -outFile 'login.html'
$loginToken = Get-Token (Get-Content 'login.html' -Raw)
Invoke-Curl "$uri/Account/Login" -method POST -body @(
    "__RequestVerificationToken=$loginToken",
    "Email=sattvikoramdhani@gmail.com",
    "Password=MasterAdmin2024!",
    "RememberMe=false"
) -outFile 'afterlogin.html'
Write-Host "Logged in as master admin"

# Load user management partial
Invoke-Curl "$uri/UserManagement/Index" -outFile 'um.html'
$umHtml = Get-Content 'um.html' -Raw
$umToken = Get-Token $umHtml
Write-Host "UM token: $umToken"

# Find user id for test email
$pattern = [regex]::Escape($regEmail) + '.*?<input[^>]*name="id"[^>]*value="(\d+)"'
$idMatch = [regex]::Match($umHtml, $pattern, 'Singleline')
if (-not $idMatch.Success) { Write-Host 'User not found in list'; exit 1 }
$userId = $idMatch.Groups[1].Value
Write-Host "Test user id: $userId"

# Update role to Operator
Invoke-Curl "$uri/UserManagement/UpdateRole" -method POST -body @(
    "__RequestVerificationToken=$umToken",
    "id=$userId",
    "role=Operator"
) -outFile 'updaterole.json'
Write-Host "UpdateRole: $(Get-Content 'updaterole.json' -Raw)"

# Toggle active (disable)
Invoke-Curl "$uri/UserManagement/ToggleActive" -method POST -body @(
    "__RequestVerificationToken=$umToken",
    "id=$userId",
    "isActive=false"
) -outFile 'toggle.json'
Write-Host "ToggleActive: $(Get-Content 'toggle.json' -Raw)"

# Delete
Invoke-Curl "$uri/UserManagement/Delete" -method POST -body @(
    "__RequestVerificationToken=$umToken",
    "id=$userId"
) -outFile 'delete.json'
Write-Host "Delete: $(Get-Content 'delete.json' -Raw)"
