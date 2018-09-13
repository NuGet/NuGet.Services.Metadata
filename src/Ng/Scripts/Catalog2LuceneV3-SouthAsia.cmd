@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2lucenev3reg2southasia.Title}"

title #{Jobs.catalog2lucenev3reg2southasia.Title}

start /w Ng.exe catalog2lucene ^
	-source #{Jobs.common.v3.Source} ^
	-luceneDirectoryType azure ^
	-luceneStorageAccountName #{Jobs.SouthAsia.v3.Storage.Name} ^
	-luceneStorageKeyValue #{Jobs.SouthAsia.v3.Storage.Key} ^
	-luceneStorageContainer #{Jobs.SouthAsia.catalog2lucenev3.LuceneContainer} ^
	-registration #{Jobs.China.catalog2lucenev3reg2.Registration} ^
	-instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
	-vaultName #{Deployment.Azure.KeyVault.VaultName} ^
	-clientId #{Deployment.Azure.KeyVault.ClientId} ^
	-certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
	-verbose true -interval #{Jobs.common.v3.Interval}

echo "Finished #{Jobs.catalog2lucenev3reg2southasia.Title}"

goto Top