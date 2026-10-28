// Registro delle immagini di SunCash. Si crea prima del push delle immagini, poi main.bicep le usa.
@description('Regione Azure.')
param location string = resourceGroup().location

@description('Nome del registro: globale, solo lettere e numeri.')
param registryName string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: { name: 'Basic' }
  properties: {
    // Utente admin: il modo più semplice perché Container Apps scarichi le immagini, per un'app personale.
    adminUserEnabled: true
  }
}

output loginServer string = registry.properties.loginServer
