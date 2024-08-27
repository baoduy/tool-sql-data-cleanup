
# [SQL Data Cleanup]()

## Overview

The SQL Data Cleanup program is designed to clean up old records from multiple SQL databases based on a provided configuration. It uses a configuration file (`appsettings.json`) to specify the databases, tables, and conditions for identifying old records. The program leverages dependency injection and Entity Framework Core to manage database connections and execute cleanup operations.

## Features

- Configurable cleanup settings for multiple databases and tables.
- Asynchronous operations for efficient database interactions.
- Dependency injection for flexible and testable code.
- Detailed logging of cleanup operations.

## Configuration

The configuration for the SQL Data Cleanup program is stored in the `appsettings.json` file. Below is an example configuration with explanations for each part:

```json
{
  "DbCleanup": {
    // Number of days to keep data before considering it old and eligible for cleanup
    "OlderThanDays": 365, // Keep Data for 1 year

    // Connection string template for connecting to the SQL databases
    "ConnectionString": "Server=tcp:random-server-name.database.windows.net,1433;Initial Catalog=[DbName];User ID=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",

    // Primary field used for identifying records in the tables
    "PrimaryField": "Id",

    // Fields used to determine the age of the records for cleanup
    "ConditionFields": ["CreatedOn", "CreatedOn"],

    // Configuration for individual databases
    "Databases": {
      // Configuration for the "random-database-1" database
      "database-1": {
        // Primary field used for identifying records in this database
        "PrimaryField": "Id",

        // Fields used to determine the age of the records for cleanup in this database
        "ConditionFields": ["CreatedOn"],

        // Configuration for individual tables within this database
        "Tables": {
          // Configuration for the "random-table-1" table
          "table-1": {
            // Primary field used for identifying records in this table
            "PrimaryField": "Id"
          },
          // Configuration for the "random-table-2" table
          "random-table-2": {
            // Primary field used for identifying records in this table
            "PrimaryField": "Id"
          }
        }
      }
    }
  }
}
```

### Configuration Explanation

- **OlderThanDays**: Specifies the number of days to retain data before it is considered old and eligible for cleanup. In this case, data older than 365 days will be cleaned up.
- **ConnectionString**: Template for the connection string used to connect to the SQL databases. The placeholder `[DbName]` will be replaced with the actual database name during runtime.
- **PrimaryField**: The primary key field used to identify records in the tables.
- **ConditionFields**: Fields used to determine the age of the records. Records older than the specified number of days in these fields will be considered for cleanup.
- **Databases**: Contains configurations for individual databases.
  - **sg-dev-Analytics-Db**: Configuration for the "sg-dev-Analytics-Db" database.
    - **PrimaryField**: The primary key field for this database.
    - **ConditionFields**: Fields used to determine the age of the records in this database.
    - **Tables**: Contains configurations for individual tables within this database.
      - **FxRateHistoriesLifeTime**: Configuration for the "FxRateHistoriesLifeTime" table.
        - **PrimaryField**: The primary key field for this table.

## Usage

### Prerequisites

- .NET 6.0 SDK or later
- SQL Server database(s) with appropriate permissions

### Setup

1. Clone the repository:
    ```sh
    git clone https://github.com/baoduy/tool-sql-data-cleanup
    cd sql-data-cleanup
    ```

2. Update the `appsettings.json` file with your database configurations.

3. Build the project:
    ```sh
    dotnet build
    ```

4. Run the project:
    ```sh
    dotnet run
    ```

### Code Structure

- **SqlDataCleanup/Config.cs**: Contains configuration classes and methods for setting up dependency injection.
- **SqlDataCleanup/DbCleanupJob.cs**: Contains the `DbCleanupJob` class, which handles the cleanup operations for individual databases.
- **SqlDataCleanup/Extensions.cs**: Contains extension methods for configuration objects.
- **SqlDataCleanup/SqlCleanupJob.cs**: Contains the `SqlCleanupJob` class, which orchestrates the cleanup operations for all configured databases.

### Example

To run the cleanup job, simply execute the program. The program will read the configuration from `appsettings.json`, connect to the specified databases, and delete old records based on the provided settings.

```sh
dotnet run
```

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or bug fixes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
