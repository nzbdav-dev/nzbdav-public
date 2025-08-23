import "react-router";
import { createRequestHandler } from "@react-router/express";
import express from "express";
import { createProxyMiddleware } from "http-proxy-middleware";
import { websocketServer } from "./websocket.server";

declare module "react-router" {
  interface AppLoadContext {
    VALUE_FROM_EXPRESS: string;
  }
}

export const app = express();
export const initializeWebsocketServer = websocketServer.initialize;

// Proxy all webdav and api requests to the backend
const forwardToBackend = createProxyMiddleware({
  target: process.env.BACKEND_URL,
  changeOrigin: true,
});

app.use((req, res, next) => {
  if (
    req.method.toUpperCase() === "PROPFIND"
    || req.method.toUpperCase() === "OPTIONS"
    || req.path.startsWith("/api")
    || req.path.startsWith("/view")
    || req.path.startsWith("/.ids")
    || req.path.startsWith("/nzbs")
    || req.path.startsWith("/content")
    || req.path.startsWith("/completed-symlinks")
  ) {
    return forwardToBackend(req, res, next);
  }
  next();
});

// Let frontend handle all other requests
app.use(
  createRequestHandler({
    build: () => import("virtual:react-router/server-build"),
    getLoadContext() {
      return {
        VALUE_FROM_EXPRESS: "Hello from Express",
      };
    },
  }),
);
