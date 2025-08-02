using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NzbWebDAV.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiProviderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Initialize multi-provider system with existing single provider if it exists
            migrationBuilder.Sql(@"
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.providers.count', '1'
                WHERE EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.host')
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.providers.count');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.providers.primary', '0'
                WHERE EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.host')
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.providers.primary');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.name', 'Primary Provider'
                WHERE EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.host')
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.name');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.host', ConfigValue
                FROM ConfigItems 
                WHERE ConfigName = 'usenet.host'
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.host');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.port', ConfigValue
                FROM ConfigItems 
                WHERE ConfigName = 'usenet.port'
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.port');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.use-ssl', ConfigValue
                FROM ConfigItems 
                WHERE ConfigName = 'usenet.use-ssl'
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.use-ssl');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.connections', ConfigValue
                FROM ConfigItems 
                WHERE ConfigName = 'usenet.connections'
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.connections');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.user', ConfigValue
                FROM ConfigItems 
                WHERE ConfigName = 'usenet.user'
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.user');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.pass', ConfigValue
                FROM ConfigItems 
                WHERE ConfigName = 'usenet.pass'
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.pass');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.priority', '0'
                WHERE EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.host')
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.priority');
                
                INSERT INTO ConfigItems (ConfigName, ConfigValue)
                SELECT 'usenet.provider.0.enabled', 'true'
                WHERE EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.host')
                AND NOT EXISTS (SELECT 1 FROM ConfigItems WHERE ConfigName = 'usenet.provider.0.enabled');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove multi-provider configuration keys
            migrationBuilder.Sql(@"
                DELETE FROM ConfigItems 
                WHERE ConfigName IN (
                    'usenet.providers.count',
                    'usenet.providers.primary'
                )
                OR ConfigName LIKE 'usenet.provider.%.%';
            ");
        }
    }
}