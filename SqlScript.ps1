$server = "sql-sweetpanda-dev-demodb-server.database.windows.net"
$database = "sql-sweetpanda-dev-demodb"
$username = Read-Host "Azure SQL admin username"
$securePassword = Read-Host "Azure SQL admin password" -AsSecureString

$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)

    $builder = New-Object Microsoft.Data.SqlClient.SqlConnectionStringBuilder
    $builder["Data Source"] = "tcp:$server,1433"
    $builder["Initial Catalog"] = $database
    $builder["User ID"] = $username
    $builder["Password"] = $password
    $builder["Encrypt"] = $true
    $builder["TrustServerCertificate"] = $false
    $builder["Connect Timeout"] = 30

    $env:ConnectionStrings__DefaultConnection = $builder.ConnectionString
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    Remove-Variable password -ErrorAction SilentlyContinue
}