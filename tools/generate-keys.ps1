# Generate RSA key pair for testing
$rsa = [System.Security.Cryptography.RSA]::Create(2048)

# Export private key
$privateKey = $rsa.ExportRSAPrivateKeyPem()
Set-Content -Path "private.pem" -Value $privateKey

# Export public key
$publicKey = $rsa.ExportRSAPublicKeyPem()
Set-Content -Path "public.pem" -Value $publicKey

Write-Host "Generated RSA key pair:"
Write-Host "  - private.pem (keep secret!)"
Write-Host "  - public.pem (distribute with app)"
