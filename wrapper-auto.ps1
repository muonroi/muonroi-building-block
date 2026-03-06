param(
  [Parameter(Mandatory=$true)][string]$Prompt
)

# 1) Cấu hình ProcessStartInfo
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "codex.exe"
$psi.Arguments = "--approval-mode suggest `"$Prompt`""
$psi.UseShellExecute        = $false   # BẮT BUỘC để redirect stream
$psi.RedirectStandardInput  = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError  = $true
$psi.CreateNoWindow         = $true
# (Có thể thêm WorkingDirectory nếu cần)
# $psi.WorkingDirectory = "D:\Personal\Project\MuonroiBuildingBlock"

$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $psi

# 2) Handler: khi có output, nếu thấy thông báo hỏi chạy lệnh thì auto "Yes"
$allowCmdRegex = [regex]::new("Allow command\?", "IgnoreCase")

$onOut = [System.Diagnostics.DataReceivedEventHandler]{
  param($sender, $e)
  if ($null -ne $e.Data) {
    Write-Host $e.Data
    if ($allowCmdRegex.IsMatch($e.Data)) {
      try { $sender.StandardInput.WriteLine("Yes") } catch {}
    }
  }
}

$onErr = [System.Diagnostics.DataReceivedEventHandler]{
  param($sender, $e)
  if ($null -ne $e.Data) {
    # Nhiều CLI in prompt sang STDERR, nên cũng dò ở đây
    Write-Host $e.Data
    if ($allowCmdRegex.IsMatch($e.Data)) {
      try { $sender.StandardInput.WriteLine("Yes") } catch {}
    }
  }
}

# 3) Start + đăng ký đọc bất đồng bộ
$null = $proc.Start()
$proc.add_OutputDataReceived($onOut)
$proc.add_ErrorDataReceived($onErr)
$proc.BeginOutputReadLine()
$proc.BeginErrorReadLine()

# 4) Đợi tiến trình kết thúc
$proc.WaitForExit()

# 5) Trả mã thoát (nếu cần dùng trong pipeline/CI)
exit $proc.ExitCode
