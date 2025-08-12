import WebSocket, { WebSocketServer } from 'ws';
import { sessionStorage } from "../app/auth/authentication.server";
import type { IncomingMessage } from 'http';

function initializeWebsocketServer(wss: WebSocketServer) {
    // keep track of socket subscriptions
    const websockets = new Map<WebSocket, string>();
    const subscriptions = new Map<string, Set<WebSocket>>();
    const lastMessage = new Map<string, string>();
    initializeWebsocketClient(subscriptions, lastMessage);

    // authenticate new websocket sessions
    wss.on("connection", async (ws: WebSocket, request: IncomingMessage) => {
        const cookieHeader = request.headers.cookie;
        if (cookieHeader) {
            try {
                const session = await sessionStorage.getSession(cookieHeader);
                const user = session.get("user");
                if (!user) {
                    console.warn("Websocket authentication failed. Sign in required.");
                    ws.close(1008, "Unauthorized");
                    return;
                }

                // handle topic subscription
                ws.onmessage = (event: WebSocket.MessageEvent) => {
                    var topic = event.data.toString();
                    websockets.set(ws, topic);
                    var topicSubscriptions = subscriptions.get(topic);
                    if (topicSubscriptions) topicSubscriptions.add(ws);
                    else subscriptions.set(topic, new Set<WebSocket>([ws]));
                    var messageToSend = lastMessage.get(topic);
                    if (messageToSend) ws.send(messageToSend);
                };

                // unsubscribe from topics
                ws.onclose = () => {
                    var topic = websockets.get(ws);
                    if (topic) {
                        websockets.delete(ws);
                        var topicSubscriptions = subscriptions.get(topic);
                        if (topicSubscriptions) topicSubscriptions.delete(ws);
                    }
                };
            } catch (error) {
                console.error("Error authenticating websocket session:", error);
                ws.close(1011, "Internal server error");
                return;
            }
        } else {
            console.warn("Websocket authentication failed. Sign in required.");
            ws.close(1008, "Unauthorized");
            return;
        }
    });
}

export function initializeWebsocketClient(subscriptions: Map<string, Set<WebSocket>>, lastMessage: Map<string, string>) {
    let reconnectRetryDelay = 1000;
    let reconnectRetryMaxDelay = 30000;
    let reconnectTimeout: NodeJS.Timeout | null = null;
    const url = getBackendWebsocketUrl();

    function connect() {
        const socket = new WebSocket(url);

        socket.onopen = () => {
            reconnectRetryDelay = 1000;
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }

            socket.send(Buffer.from(process.env.FRONTEND_BACKEND_API_KEY!, "utf-8"), { binary: false });
        };

        socket.onmessage = (event: WebSocket.MessageEvent) => {
            var rawMessage = event.data.toString();
            var topicMessage = JSON.parse(rawMessage);
            var [topic, message] = [topicMessage.Topic, topicMessage.Message];
            if (!topic || !message) return;
            lastMessage.set(topic, message);
            var subscribed = subscriptions.get(topic) || [];
            subscribed.forEach(client => {
                if (client.readyState === client.OPEN) {
                    client.send(message);
                }
            });
        };

        socket.onerror = (event: WebSocket.ErrorEvent) => {
            console.error('WebSocket error:', event.message);
        };

        socket.onclose = (event: WebSocket.CloseEvent) => {
            console.info(`WebSocket closed (code: ${event.code}, reason: ${event.reason}) — retrying in ${reconnectRetryDelay / 1000}s`);
            scheduleReconnect();
        };
    }

    function scheduleReconnect() {
        if (reconnectTimeout) return;
        reconnectTimeout = setTimeout(() => {
            reconnectRetryDelay = Math.min(reconnectRetryDelay * 2, reconnectRetryMaxDelay);
            connect();
        }, reconnectRetryDelay);
    }

    connect();
}

function getBackendWebsocketUrl() {
    const host = process.env.BACKEND_URL!;
    return `${host.replace(/\/$/, '')}/ws`.replace(/^http/, 'ws');
}

export const websocketServer = {
    initialize: initializeWebsocketServer
}