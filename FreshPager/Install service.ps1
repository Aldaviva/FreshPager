$binaryPathName = Resolve-Path(join-path $PSScriptRoot "FreshPager.exe")

New-Service -Name "FreshPager" -DisplayName "FreshPager" -Description "When Freshping detects that a check is down, it sends a webhook request to this server, which triggers a PagerDuty alert for the configured service." -BinaryPathName $binaryPathName.Path -DependsOn Tcpip
sc.exe failure FreshPager actions= restart/0/restart/0/restart/0 reset= 86400