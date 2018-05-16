@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.monitoringstatusreserializer.Title}"

	title #{Jobs.monitoringstatusreserializer.Title}
    
	start /w .\Ng.exe monitoringstatusreserializer ^
		-statusFolder #{Jobs.endpointmonitoring.StatusFolder} ^
		-storageType azure -storageAccountName ^
		#{Jobs.common.v3.c2r.StorageAccountName} ^
		-storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
		-storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} ^
		-storageTypeAuditing azure ^
		-storageAccountNameAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountName} ^
		-storageKeyValueAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountKey} ^
		-storageContainerAuditing auditing ^
		-storagePathAuditing package ^
		-instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
		-vaultName #{Deployment.Azure.KeyVault.VaultName} ^
		-clientId #{Deployment.Azure.KeyVault.ClientId} ^
		-certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
		-verbose true

	echo "Finished #{Jobs.monitoringstatusreserializer.Title}"

	goto Top
