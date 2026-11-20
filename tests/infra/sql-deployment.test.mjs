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

test('explicit provisioning separates approved Basic from free serverless defaults', () => {
    const inner = sql.properties.template;
    const db = allSql.find(r => r.resource.type === 'Microsoft.Sql/servers/databases').resource;
    assert.equal(template.parameters.useBasicSql.defaultValue, false);
    assert.equal(inner.parameters.useBasicSql.defaultValue, false);
    assert.equal(db.sku, "[if(parameters('useBasicSql'), variables('basicSku'), variables('serverlessSku'))]");
    assert.equal(db.properties, "[if(parameters('useBasicSql'), variables('basicProperties'), variables('serverlessProperties'))]");
    assert.deepEqual(inner.variables.basicSku, {name: 'Basic', tier: 'Basic', capacity: 5});
    assert.deepEqual(inner.variables.basicProperties, {maxSizeBytes: 2147483648, useFreeLimit: false});
    assert.equal(inner.variables.serverlessProperties.useFreeLimit, true);
    assert.equal(inner.variables.serverlessProperties.freeLimitExhaustionBehavior, 'AutoPause');
    assert.equal(inner.variables.serverlessProperties.autoPauseDelay, 60);
    assert.equal(inner.variables.serverlessSku.name, 'GP_S_Gen5');
});

test('daytime cron retains night scale-to-zero and both daylight-saving offsets', () => {
    const app = resourcesOf(template).find(r => r.type === 'Microsoft.App/containerApps');
    assert.equal(app.properties.template.scale.minReplicas, 0);
    assert.equal(app.properties.template.scale.maxReplicas, 1);
    assert.ok(app.properties.template.scale.rules.includes("parameters('enableDaytimeWarmup')"));
    const rule = template.variables.daytimeWarmRule;
    assert.equal(rule.custom.type, 'cron');
    assert.deepEqual(rule.custom.metadata, {timezone: 'Europe/Paris', start: '59 7 * * *', end: '0 20 * * *', desiredReplicas: '1'});
    const localHour = iso => Number(new Intl.DateTimeFormat('en-GB', {timeZone: rule.custom.metadata.timezone, hour:'2-digit',hourCycle:'h23'}).format(new Date(iso)));
    assert.equal(localHour('2026-01-15T07:00:00Z'), 8);
    assert.equal(localHour('2026-07-15T06:00:00Z'), 8);
    assert.equal(localHour('2026-01-15T19:00:00Z'), 20);
    assert.equal(localHour('2026-07-15T18:00:00Z'), 20);
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
