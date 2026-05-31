#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { spawn } from "child_process";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const server = new Server(
  {
    name: "test-runner",
    version: "1.0.0",
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "run_test",
        description: "Run the test script with a specific command",
        inputSchema: {
          type: "object",
          properties: {
            command: {
              type: "string",
              enum: [
                "build",
                "build-client",
                "integration",
                "e2e",
                "all",
                "logs",
                "clean",
                "docker-errors",
              ],
              description: "The command to run with ./test.sh",
            },
          },
          required: ["command"],
        },
      },
    ],
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  if (request.params.name !== "run_test") {
    throw new Error("Unknown tool");
  }

  const { command } = request.params.arguments;
  const scriptPath = path.join(__dirname, "test.sh");

  return new Promise((resolve) => {
    const child = spawn(scriptPath, [command], {
      cwd: __dirname,
      env: { ...process.env, PATH: process.env.PATH },
    });

    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (data) => {
      stdout += data.toString();
    });

    child.stderr.on("data", (data) => {
      stderr += data.toString();
    });

    child.on("close", (code) => {
      resolve({
        content: [
          {
            type: "text",
            text: `Command: ./test.sh ${command}\nExit Code: ${code}\n\nSTDOUT:\n${stdout}\n\nSTDERR:\n${stderr}`,
          },
        ],
        isError: code !== 0,
      });
    });
  });
});

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((error) => {
  console.error("Server error:", error);
  process.exit(1);
});
