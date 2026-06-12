const { Client } = require('@modelcontextprotocol/sdk/client/index.js');
const { StdioClientTransport } = require('@modelcontextprotocol/sdk/client/stdio.js');
const path = require('path');

async function main() {
  const transport = new StdioClientTransport({
    command: 'node',
    args: [path.resolve('../muonroi-docs/mcp/src/server.js')],
  });

  const client = new Client(
    { name: 'repro-client', version: '1.0.0' },
    { capabilities: {} }
  );

  await client.connect(transport);

  try {
    console.log('Calling docs.search...');
    const result = await client.callTool({
      name: 'docs.search',
      arguments: { query: 'test' }
    });
    console.log('Result:', JSON.stringify(result, null, 2));
  } catch (err) {
    console.error('Error:', err);
  } finally {
    await transport.close();
  }
}

main().catch(console.error);
