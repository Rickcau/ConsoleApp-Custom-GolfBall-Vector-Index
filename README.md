# Golf Ball Vector Search Application

## Overview
This console application demonstrates an advanced search capability for golf ball specifications using Azure Cognitive Search with vector embeddings. It allows users to search through a database of golf balls using natural language queries, making it easier to find specific golf balls based on their characteristics, markings, and performance attributes.

## Features
- **Vector-Based Search**: Utilizes Azure OpenAI embeddings for semantic search capabilities
- **Natural Language Queries**: Supports conversational search queries
- **Comprehensive Golf Ball Data**: Includes information about:
  - Manufacturer details
  - USGA lot numbers
  - Pole and seam markings
  - Ball colors
  - Construction codes
  - Ball specifications
  - Dimple counts
  - Spin characteristics
  - Image URLs (where available)

## Technical Implementation
- Built with .NET Core
- Integrates with Azure Cognitive Search
- Uses Azure OpenAI for vector embeddings
- Implements efficient data indexing and search capabilities
- Configurable through appsettings.json

## Data Sources
The application uses a sample dataset that combines:
- Publicly available golf ball specifications
- Data sourced from USGA.com website
- Fictitious sample data for demonstration purposes

**Note**: While some of the data is sourced from public domains and USGA.com, portions of the dataset are fictional and used for demonstration purposes only. The data structure and search capabilities represent real-world scenarios, but the specific golf ball entries may include synthetic data for testing and demonstration.

## Sample Queries
The application supports various types of natural language queries, such as:
- "Find golf balls with similar markings to Titleist Pro V1"
- "Show me white golf balls with arrow markings"
- "Find golf balls with high spin characteristics"

## Prerequisites
- .NET Core SDK
- Azure Cognitive Search instance
- Azure OpenAI API access
- Proper configuration in appsettings.json

## Configuration
The application requires proper configuration of:
- Azure Search Service endpoint
- Azure OpenAI endpoint
- API keys
- Index name
- Model configurations

## Legal Notice
This application is for demonstration purposes only. While it uses some publicly available data from USGA.com and other public sources, much of the data is fictional and should not be used for actual golf ball verification or official purposes. 