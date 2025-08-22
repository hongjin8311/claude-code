#!/usr/bin/env python3
"""
Azure OpenAI Resource Scanner
Scans all Azure OpenAI resources and collects model deployment information.
"""

import json
import csv
from datetime import datetime
from typing import List, Dict, Any
import argparse
import sys

from azure.identity import DefaultAzureCredential, InteractiveBrowserCredential
from azure.mgmt.cognitiveservices import CognitiveServicesManagementClient
from azure.mgmt.resource import ResourceManagementClient, SubscriptionClient
import requests


class AzureOpenAIScanner:
    def __init__(self, tenant_id: str = None):
        """Initialize the scanner with Azure credentials."""
        if tenant_id:
            self.credential = InteractiveBrowserCredential(tenant_id=tenant_id)
        else:
            self.credential = DefaultAzureCredential()
        
        self.subscription_client = SubscriptionClient(self.credential)
        self.openai_resources = []
        
    def get_subscriptions(self) -> List[str]:
        """Get all available subscriptions."""
        subscriptions = []
        try:
            for sub in self.subscription_client.subscriptions.list():
                subscriptions.append(sub.subscription_id)
                print(f"Found subscription: {sub.display_name} ({sub.subscription_id})")
        except Exception as e:
            print(f"Error getting subscriptions: {e}")
        
        return subscriptions
    
    def get_openai_resources(self, subscription_id: str) -> List[Dict[str, Any]]:
        """Get all OpenAI resources in a subscription."""
        resources = []
        try:
            resource_client = ResourceManagementClient(self.credential, subscription_id)
            cognitive_client = CognitiveServicesManagementClient(self.credential, subscription_id)
            
            # Get all cognitive services accounts
            for account in cognitive_client.accounts.list():
                if account.kind.lower() == 'openai':
                    resource_info = {
                        'subscription_id': subscription_id,
                        'resource_group': account.id.split('/')[4],
                        'name': account.name,
                        'location': account.location,
                        'sku': account.sku.name if account.sku else None,
                        'endpoint': account.properties.endpoint if account.properties else None,
                        'deployments': []
                    }
                    
                    # Get deployments for this OpenAI resource
                    try:
                        deployments = cognitive_client.deployments.list(
                            resource_group_name=resource_info['resource_group'],
                            account_name=account.name
                        )
                        
                        for deployment in deployments:
                            deployment_info = {
                                'name': deployment.name,
                                'model': deployment.properties.model.name if deployment.properties and deployment.properties.model else None,
                                'model_version': deployment.properties.model.version if deployment.properties and deployment.properties.model else None,
                                'sku_name': deployment.sku.name if deployment.sku else None,
                                'sku_capacity': deployment.sku.capacity if deployment.sku else None,
                                'scale_type': deployment.properties.scale_settings.scale_type if deployment.properties and deployment.properties.scale_settings else None
                            }
                            resource_info['deployments'].append(deployment_info)
                            
                    except Exception as e:
                        print(f"Error getting deployments for {account.name}: {e}")
                    
                    resources.append(resource_info)
                    print(f"Found OpenAI resource: {account.name} in {account.location}")
                    
        except Exception as e:
            print(f"Error scanning subscription {subscription_id}: {e}")
            
        return resources
    
    def scan_all_resources(self) -> List[Dict[str, Any]]:
        """Scan all subscriptions for OpenAI resources."""
        all_resources = []
        subscriptions = self.get_subscriptions()
        
        if not subscriptions:
            print("No subscriptions found or accessible.")
            return all_resources
            
        for subscription_id in subscriptions:
            print(f"\nScanning subscription: {subscription_id}")
            resources = self.get_openai_resources(subscription_id)
            all_resources.extend(resources)
            
        return all_resources
    
    def save_to_json(self, resources: List[Dict[str, Any]], filename: str = None):
        """Save resources to JSON file."""
        if not filename:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"azure_openai_resources_{timestamp}.json"
            
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump({
                'scan_date': datetime.now().isoformat(),
                'total_resources': len(resources),
                'resources': resources
            }, f, indent=2, ensure_ascii=False)
            
        print(f"JSON report saved to: {filename}")
        return filename
    
    def save_to_csv(self, resources: List[Dict[str, Any]], filename: str = None):
        """Save resources to CSV file."""
        if not filename:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"azure_openai_resources_{timestamp}.csv"
            
        with open(filename, 'w', newline='', encoding='utf-8') as f:
            fieldnames = [
                'subscription_id', 'resource_group', 'resource_name', 'location', 'sku',
                'endpoint', 'deployment_name', 'model_name', 'model_version', 
                'sku_name', 'sku_capacity', 'scale_type'
            ]
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            writer.writeheader()
            
            for resource in resources:
                if resource['deployments']:
                    for deployment in resource['deployments']:
                        row = {
                            'subscription_id': resource['subscription_id'],
                            'resource_group': resource['resource_group'],
                            'resource_name': resource['name'],
                            'location': resource['location'],
                            'sku': resource['sku'],
                            'endpoint': resource['endpoint'],
                            'deployment_name': deployment['name'],
                            'model_name': deployment['model'],
                            'model_version': deployment['model_version'],
                            'sku_name': deployment['sku_name'],
                            'sku_capacity': deployment['sku_capacity'],
                            'scale_type': deployment['scale_type']
                        }
                        writer.writerow(row)
                else:
                    # Resource without deployments
                    row = {
                        'subscription_id': resource['subscription_id'],
                        'resource_group': resource['resource_group'],
                        'resource_name': resource['name'],
                        'location': resource['location'],
                        'sku': resource['sku'],
                        'endpoint': resource['endpoint'],
                        'deployment_name': '',
                        'model_name': '',
                        'model_version': '',
                        'sku_name': '',
                        'sku_capacity': '',
                        'scale_type': ''
                    }
                    writer.writerow(row)
                    
        print(f"CSV report saved to: {filename}")
        return filename
    
    def print_summary(self, resources: List[Dict[str, Any]]):
        """Print a summary of found resources."""
        print(f"\n{'='*60}")
        print("AZURE OPENAI RESOURCES SUMMARY")
        print(f"{'='*60}")
        print(f"Total Resources Found: {len(resources)}")
        
        if not resources:
            print("No Azure OpenAI resources found.")
            return
            
        total_deployments = sum(len(r['deployments']) for r in resources)
        print(f"Total Deployments: {total_deployments}")
        
        # Group by location
        locations = {}
        for resource in resources:
            location = resource['location']
            if location not in locations:
                locations[location] = 0
            locations[location] += 1
            
        print(f"\nResources by Location:")
        for location, count in sorted(locations.items()):
            print(f"  {location}: {count}")
            
        # Group by model
        models = {}
        for resource in resources:
            for deployment in resource['deployments']:
                model = deployment['model']
                if model:
                    if model not in models:
                        models[model] = 0
                    models[model] += 1
                    
        if models:
            print(f"\nDeployments by Model:")
            for model, count in sorted(models.items()):
                print(f"  {model}: {count}")


def main():
    parser = argparse.ArgumentParser(description='Scan Azure OpenAI resources')
    parser.add_argument('--tenant-id', help='Azure tenant ID for authentication')
    parser.add_argument('--output-json', help='JSON output filename')
    parser.add_argument('--output-csv', help='CSV output filename')
    parser.add_argument('--no-summary', action='store_true', help='Skip printing summary')
    
    args = parser.parse_args()
    
    try:
        print("Initializing Azure OpenAI Scanner...")
        scanner = AzureOpenAIScanner(tenant_id=args.tenant_id)
        
        print("Scanning for Azure OpenAI resources...")
        resources = scanner.scan_all_resources()
        
        if not args.no_summary:
            scanner.print_summary(resources)
        
        # Save results
        json_file = scanner.save_to_json(resources, args.output_json)
        csv_file = scanner.save_to_csv(resources, args.output_csv)
        
        print(f"\nScan completed successfully!")
        print(f"Results saved to:")
        print(f"  - JSON: {json_file}")
        print(f"  - CSV: {csv_file}")
        
    except KeyboardInterrupt:
        print("\nScan interrupted by user.")
        sys.exit(1)
    except Exception as e:
        print(f"Error during scan: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()