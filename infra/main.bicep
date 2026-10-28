// SunCash su Azure Container Apps (docs/deploy-azure.md).
// Un'app con due container nella stessa rete: «api» (ASP.NET Core + job) e «web» (nginx + Angular).
// Una sola replica, sempre accesa: SQLite vuole un solo processo che scrive e i job devono girare.
// Il database sta sul disco locale del container; i backup su Azure Files, da cui l'app riparte
// a ogni riavvio (DatabaseRestore). SQLite non apre mai un file su Azure Files.

@description('Regione Azure.')
param location string = resourceGroup().location

@description('Prefisso dei nomi delle risorse.')
param name string = 'suncash'

@description('Registro creato da registry.bicep.')
param registryName string

@description('Immagine dell\'API, con tag.')
param apiImage string

@description('Immagine del frontend, con tag.')
param webImage string

@description('Primo giorno dello storico (yyyy-MM-dd).')
param syncStartDate string = '2026-01-01'

@description('Zona di mercato del prezzo zonale.')
param valuationZone string = 'NORD'

@description('Indirizzo Northbound di FusionSolar, es. https://<prefisso>.fusionsolar.huawei.com/thirdData/')
param fusionSolarBaseUrl string

@secure()
param fusionSolarUserName string

@secure()
param fusionSolarSystemCode string

@secure()
param fusionSolarStationCode string

@description('Facoltative: senza, l\'app parte e la pagina Stato segnala che mancano.')
@secure()
param gmeUserName string = ''

@secure()
param gmePassword string = ''

@description('Registrazione dell\'app per il login Microsoft. Vuoto = app non esposta su internet.')
param authClientId string = ''

@secure()
param authClientSecret string = ''

@description('Tenant Entra ID del login.')
param authTenantId string = subscription().tenantId

var loginEnabled = !empty(authClientId) && !empty(authClientSecret)
var gmeEnabled = !empty(gmeUserName) && !empty(gmePassword)

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${name}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take('${replace(name, '-', '')}${uniqueString(resourceGroup().id)}', 24)
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource backupShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: '${storage.name}/default/backups'
  properties: { shareQuota: 5 }
}

resource appEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${name}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource environmentBackups 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: appEnvironment
  name: 'backups'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: 'backups'
      accessMode: 'ReadWrite'
    }
  }
  dependsOn: [backupShare]
}

var baseSecrets = [
  { name: 'registry-password', value: registry.listCredentials().passwords[0].value }
  { name: 'fusionsolar-username', value: fusionSolarUserName }
  { name: 'fusionsolar-systemcode', value: fusionSolarSystemCode }
  { name: 'fusionsolar-stationcode', value: fusionSolarStationCode }
]
var gmeSecrets = gmeEnabled ? [
  { name: 'gme-username', value: gmeUserName }
  { name: 'gme-password', value: gmePassword }
] : []
var authSecrets = loginEnabled ? [
  { name: 'microsoft-provider-authentication-secret', value: authClientSecret }
] : []

var baseEnv = [
  { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
  { name: 'ConnectionStrings__SunCash', value: 'Data Source=/data/suncash.db' }
  { name: 'Backup__Directory', value: '/backups' }
  { name: 'Sync__StartDate', value: syncStartDate }
  { name: 'Valuation__Zone', value: valuationZone }
  { name: 'Energy__Provider', value: 'FusionSolar' }
  { name: 'Energy__FusionSolar__BaseUrl', value: fusionSolarBaseUrl }
  { name: 'Energy__FusionSolar__UserName', secretRef: 'fusionsolar-username' }
  { name: 'Energy__FusionSolar__SystemCode', secretRef: 'fusionsolar-systemcode' }
  { name: 'Energy__FusionSolar__StationCode', secretRef: 'fusionsolar-stationcode' }
]
var gmeEnv = gmeEnabled ? [
  { name: 'Gme__UserName', secretRef: 'gme-username' }
  { name: 'Gme__Password', secretRef: 'gme-password' }
] : []

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  properties: {
    managedEnvironmentId: appEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        // Senza login l'app resta raggiungibile solo dentro l'ambiente: mai aperta su internet.
        external: loginEnabled
        targetPort: 80
        transport: 'auto'
      }
      registries: [
        {
          server: registry.properties.loginServer
          username: registry.listCredentials().username
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: concat(baseSecrets, gmeSecrets, authSecrets)
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: concat(baseEnv, gmeEnv)
          volumeMounts: [
            { volumeName: 'data', mountPath: '/data' }
            { volumeName: 'backups', mountPath: '/backups' }
          ]
        }
        {
          name: 'web'
          image: webImage
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'API_UPSTREAM', value: 'localhost:8080' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [
        { name: 'data', storageType: 'EmptyDir' }
        { name: 'backups', storageType: 'AzureFile', storageName: environmentBackups.name }
      ]
    }
  }
}

resource auth 'Microsoft.App/containerApps/authConfigs@2024-03-01' = if (loginEnabled) {
  parent: app
  name: 'current'
  properties: {
    platform: { enabled: true }
    globalValidation: {
      unauthenticatedClientAction: 'RedirectToLoginPage'
      redirectToProvider: 'azureactivedirectory'
    }
    identityProviders: {
      azureActiveDirectory: {
        registration: {
          clientId: authClientId
          clientSecretSettingName: 'microsoft-provider-authentication-secret'
          openIdIssuer: '${environment().authentication.loginEndpoint}${authTenantId}/v2.0'
        }
      }
    }
  }
}

@description('Indirizzo pubblico dell\'app (attivo solo con il login configurato).')
output url string = 'https://${app.name}.${appEnvironment.properties.defaultDomain}'

@description('Indirizzo di ritorno da registrare nell\'app di login.')
output loginRedirectUri string = 'https://${app.name}.${appEnvironment.properties.defaultDomain}/.auth/login/aad/callback'
