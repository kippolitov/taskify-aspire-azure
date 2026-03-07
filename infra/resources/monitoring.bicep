targetScope = 'resourceGroup'

// === PARAMETERS ===

@minLength(1)
@maxLength(20)
@description('Environment name (dev, prod)')
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@minLength(3)
@maxLength(8)
@description('Unique identifier suffix to prevent naming conflicts')
param uniqueId string

@description('Email addresses for budget alerts (comma-separated)')
param budgetAlertEmails string = ''

// === RESOURCES ===

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-taskify-${environmentName}-${uniqueId}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-taskify-${environmentName}-${uniqueId}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    RetentionInDays: 90
    SamplingPercentage: 100
  }
}

// Cost management action group for budget alerts
resource budgetActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (budgetAlertEmails != '') {
  name: 'ag-budget-taskify-${environmentName}-${uniqueId}'
  location: 'global'
  properties: {
    groupShortName: 'BudgetAlert'
    enabled: true
    emailReceivers: [for email in split(budgetAlertEmails, ','): {
      name: 'Email-${replace(email, '@', '-at-')}'
      emailAddress: trim(email)
      useCommonAlertSchema: true
    }]
  }
}

// Temporarily disabled metric alerts due to aggregation type validation issues
// TODO: Re-enable after determining correct metric aggregation types
/*
// Metric alert for Application Insights exceptions
resource exceptionAlertRule 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-exceptions-taskify-${environmentName}-${uniqueId}'
  location: 'global'
  properties: {
    description: 'Alert when exception count exceeds threshold'
    severity: 2
    enabled: true
    scopes: [
      applicationInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ExceptionCount'
          metricName: 'exceptions/count'
          operator: 'GreaterThan'
          threshold: 10
          timeAggregation: 'Total'
        }
      ]
    }
    actions: budgetAlertEmails != '' ? [
      {
        actionGroupId: budgetActionGroup.id
      }
    ] : []
  }
}

// Metric alert for high response time
resource responseTimeAlertRule 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-response-time-taskify-${environmentName}-${uniqueId}'
  location: 'global'
  properties: {
    description: 'Alert when API response time is too high'
    severity: 3
    enabled: true
    scopes: [
      applicationInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ResponseTime'
          metricName: 'requests/duration'
          operator: 'GreaterThan'
          threshold: 2000  // 2 seconds
          timeAggregation: 'Average'
        }
      ]
    }
    actions: budgetAlertEmails != '' ? [
      {
        actionGroupId: budgetActionGroup.id
      }
    ] : []
  }
}

// Metric alert for availability drop
resource availabilityAlertRule 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-availability-taskify-${environmentName}-${uniqueId}'
  location: 'global'
  properties: {
    description: 'Alert when application availability drops below 99%'
    severity: 1
    enabled: true
    scopes: [
      applicationInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'Availability'
          metricName: 'availabilityResults/availabilityPercentage'
          operator: 'LessThan'
          threshold: 99
          timeAggregation: 'Average'
        }
      ]
    }
    actions: budgetAlertEmails != '' ? [
      {
        actionGroupId: budgetActionGroup.id
      }
    ] : []
  }
}
*/

// === OUTPUTS ===

@description('Log Analytics Workspace resource ID')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('Log Analytics Workspace customer ID')
output logAnalyticsWorkspaceCustomerId string = logAnalyticsWorkspace.properties.customerId

@description('Application Insights connection string')
@secure()
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString

@description('Application Insights instrumentation key (legacy)')
output applicationInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
