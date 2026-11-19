import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Test the compiled ARM contract, not Bicep text. Pass the build output as an environment variable.
const template = JSON.parse(readFileSync(process.env.SQL_DEPLOYMENT_TEMPLATE, 'utf8').replace(/^\uFEFF/, ''));
const resourcesOf = template => Object.values(template.resources ?? {});
const sql = resourcesOf(template).find(r => r.name === 'sql' && r.type === 'Microsoft.Resources/deployments');
const allSql = [];
function visit(resources, guarded = false) {
    for (const resource of resources) {
        const insideSqlGate = guarded || resource === sql;
        if (resource.type.startsWith('Microsoft.Sql/')) allSql.push({ resource, guarded: insideSqlGate });
        if (resource.properties?.template) visit(resourcesOf(resource.properties.template), insideSqlGate);
    }
}
visit(resourcesOf(template));

test('ordinary deployments cannot write any SQL resource', () => {
    assert.equal(template.parameters.provisionSqlInfrastructure.defaultValue, false);
    assert.equal(sql.condition, "[parameters('provisionSqlInfrastructure')]");
    assert.equal(allSql.length, 4);
    assert.ok(allSql.every(r => r.guarded));
    assert.equal(sql.properties.mode, 'Incremental');
});

test('explicit SQL provisioning keeps free limit, auto-pause and serverless capacity', () => {
    const db = allSql.find(r => r.resource.type === 'Microsoft.Sql/servers/databases').resource;
    assert.equal(db.properties.useFreeLimit, true);
    assert.equal(db.properties.freeLimitExhaustionBehavior, 'AutoPause');
    assert.equal(db.properties.autoPauseDelay, 60);
    assert.equal(db.properties.minCapacity, "[json('0.5')]");
    assert.equal(db.properties.maxSizeBytes, 34359738368);
    assert.equal(db.sku.name, 'GP_S_Gen5');
    assert.equal(db.sku.capacity, 2);
});

test('outputs and application connection retain the same SQL names without reading SQL', () => {
    assert.equal(template.outputs.sqlServerName.value, "[variables('sqlServerName')]");
    assert.equal(template.outputs.sqlDatabaseName.value, "[variables('sqlDatabaseName')]");
    const app = resourcesOf(template).find(r => r.type === 'Microsoft.App/containerApps');
    const env = app.properties.template.containers[0].env;
    assert.ok(env.includes("'ConnectionStrings__RecipeDb', 'value', format('Server=tcp:{0},1433;Database={1};"));
    assert.ok(env.includes("variables('sqlFqdn'), variables('sqlDatabaseName')"));
    assert.ok(env.includes("'SKIP_SQL_WAIT', 'value', 'true'"));
});
