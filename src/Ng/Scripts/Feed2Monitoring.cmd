@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.feed2monitoring.Title}"

	title #{Jobs.feed2monitoring.Title}
    
	start /w .\Ng.exe feed2monitoring -gallery #{Jobs.common.v3.f2c.Gallery} -statusFolder #{Jobs.endpointmonitoring.StatusFolder} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} -storageQueueName #{Jobs.endpointmonitoring.PackageValidatorQueue} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.feed2monitoring.Interval}

	echo "Finished #{Jobs.feed2monitoring.Title}"

	goto Top