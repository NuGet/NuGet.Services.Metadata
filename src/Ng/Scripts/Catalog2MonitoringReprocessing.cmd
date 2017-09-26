@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2monitoringreprocessing.Title}"

	title #{Jobs.catalog2monitoringreprocessing.Title}
    
	start /w .\Ng.exe catalog2monitoringreprocessing -source #{Jobs.common.v3.Source} -statusFolder #{Jobs.endpointmonitoring.StatusFolder} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} -storageQueueName #{Jobs.endpointmonitoring.PackageValidatorQueue} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2monitoringreprocessing.Title}"

	goto Top