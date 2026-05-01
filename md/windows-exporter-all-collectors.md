# Windows Exporter Collectors

This project scrapes the local Windows exporter through:

```yaml
job_name: loanflow-windows
target: host.docker.internal:9182
```

For production, Prometheus uses an explicit collector filter so the target stays predictable:

```text
cpu,logical_disk,memory,net,os,physical_disk,service,system
```

Do not put every collector in the Prometheus `collect[]` parameter unless the exporter service has actually enabled every collector; requesting a disabled collector can make the scrape return `400 Bad Request`.

## Full Collector List

Current upstream collectors:

```text
ad,adcs,adfs,cache,cpu,cpu_info,container,diskdrive,dfsr,dhcp,dns,exchange,file,fsrmquota,gpu,hyperv,iis,license,logical_disk,memory,mscluster,msmq,mssql,netframework,net,os,pagefile,performancecounter,physical_disk,printer,process,remote_fx,scheduled_task,service,smb,smbclient,smtp,system,tcp,terminal_services,textfile,time,udp,update,vmware
```

## Enable Everything For Lab/Test Only

Run an elevated PowerShell or CMD prompt on the Windows host and reinstall or update the service with all collectors enabled:

```powershell
msiexec /i windows_exporter.msi --% ENABLED_COLLECTORS=ad,adcs,adfs,cache,cpu,cpu_info,container,diskdrive,dfsr,dhcp,dns,exchange,file,fsrmquota,gpu,hyperv,iis,license,logical_disk,memory,mscluster,msmq,mssql,netframework,net,os,pagefile,performancecounter,physical_disk,printer,process,remote_fx,scheduled_task,service,smb,smbclient,smtp,system,tcp,terminal_services,textfile,time,udp,update,vmware
```

For learning, a smaller useful expansion over defaults is:

```powershell
msiexec /i windows_exporter.msi --% ENABLED_COLLECTORS="[defaults],process,tcp,udp,pagefile,cpu_info,diskdrive,physical_disk,scheduled_task,update"
```

## Verify

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:9182/health
Invoke-WebRequest -UseBasicParsing http://localhost:9182/metrics
```

After changing the Windows exporter service, restart Prometheus:

```powershell
docker compose --profile full restart prometheus grafana
```
