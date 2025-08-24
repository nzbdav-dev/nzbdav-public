import { redirect } from "react-router";
import type { Route } from "./+types/route";
import { sessionStorage } from "~/auth/authentication.server";
import styles from "./route.module.css"
import { Alert } from 'react-bootstrap';
import { backendClient, type HistorySlot, type QueueSlot } from "~/clients/backend-client.server";
import { EmptyQueue } from "./components/empty-queue/empty-queue";
import { HistoryTable } from "./components/history-table/history-table";
import { QueueTable } from "./components/queue-table/queue-table";
import { useEffect, useState } from "react";
import { receiveMessage } from "~/utils/websocket-util";

const topicNames = {
    queueItemStatus: 'qs',
    queueItemPercentage: 'qp',
    queueItemAdded: 'qa',
    queueItemRemoved: 'qr',
    historyItemAdded: 'ha',
    historyItemRemoved: 'hr',
}
const topicSubscriptions = {
    [topicNames.queueItemStatus]: 'state',
    [topicNames.queueItemPercentage]: 'state',
    [topicNames.queueItemAdded]: 'event',
    [topicNames.queueItemRemoved]: 'event',
    [topicNames.historyItemAdded]: 'event',
    [topicNames.historyItemRemoved]: 'event',
}

type BodyProps = {
    queueSlots: QueueSlot[],
    historySlots: HistorySlot[],
    error?: string,
};

export async function loader({ request }: Route.LoaderArgs) {
    var queuePromise = backendClient.getQueue();
    var historyPromise = backendClient.getHistory();
    var queue = await queuePromise;
    var history = await historyPromise;
    return {
        queueSlots: queue?.slots || [],
        historySlots: history?.slots || [],
    }
}

export default function Queue(props: Route.ComponentProps) {
    const [queueSlots, setQueueSlots] = useState(props.loaderData.queueSlots);
    const [historySlots, setHistorySlots] = useState(props.loaderData.historySlots);
    const error = props.actionData?.error;


    useEffect(() => {
        let ws: WebSocket;
        let disposed = false;
        function connect() {
            ws = new WebSocket(window.location.origin.replace(/^http/, 'ws'));
            ws.onmessage = receiveMessage(onMessage);
            ws.onopen = () => { ws.send(JSON.stringify(topicSubscriptions)); }
            ws.onclose = () => { !disposed && setTimeout(() => connect(), 1000); };
            ws.onerror = () => { ws.close() };
            return () => { disposed = true; ws.close(); }
        }

        function onMessage(topic: string, message: string) {
            if (topic == topicNames.queueItemStatus)
                onChangeQueueSlotStatus(message);
            if (topic == topicNames.queueItemPercentage)
                onChangeQueueSlotPercentage(message);
            else if (topic == topicNames.queueItemAdded)
                onAddQueueSlot(JSON.parse(message));
            else if (topic == topicNames.queueItemRemoved)
                onRemoveQueueSlot(message);
            else if (topic == topicNames.historyItemAdded)
                onAddHistorySlot(JSON.parse(message));
            else if (topic == topicNames.historyItemRemoved)
                onRemoveHistorySlot(message);
        }

        function onChangeQueueSlotStatus(message: string) {
            const [nzo_id, status] = message.split('|');
            setQueueSlots(slots => slots.map(x => x.nzo_id === nzo_id ? { ...x, status } : x))
        }

        function onChangeQueueSlotPercentage(message: string) {
            const [nzo_id, percentage] = message.split('|');
            setQueueSlots(slots => slots.map(x => x.nzo_id === nzo_id ? { ...x, percentage } : x))
        }

        function onAddQueueSlot(queueSlot: QueueSlot) {
            setQueueSlots(slots => [...slots, queueSlot])
        }

        function onRemoveQueueSlot(id: string) {
            setQueueSlots(slots => slots.filter(x => x.nzo_id !== id));
        }

        function onAddHistorySlot(historySlot: HistorySlot) {
            setHistorySlots(slots => [historySlot, ...slots])
        }

        function onRemoveHistorySlot(id: string) {
            setHistorySlots(slots => slots.filter(x => x.nzo_id !== id));
        }

        return connect();
    }, [setQueueSlots, setHistorySlots]);

    return (
        <Body queueSlots={queueSlots} historySlots={historySlots} error={error} />
    );
}

function Body({ queueSlots, historySlots, error }: BodyProps) {
    return (
        <div className={styles.container}>
            {/* queue */}
            <div className={styles.section}>
                <h3 className={styles["section-title"]}>
                    Queue
                </h3>
                <div className={styles["section-body"]}>
                    {/* error message */}
                    {error &&
                        <Alert variant="danger">
                            {error}
                        </Alert>
                    }
                    {queueSlots.length > 0 ? <QueueTable queueSlots={queueSlots} /> : <EmptyQueue />}
                </div>
            </div>

            {/* history */}
            {historySlots.length > 0 &&
                <div className={styles.section}>
                    <h3 className={styles["section-title"]}>
                        History
                    </h3>
                    <div className={styles["section-body"]}>
                        <HistoryTable historySlots={historySlots} />
                    </div>
                </div>
            }
        </div>
    );
}

export async function action({ request }: Route.ActionArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    try {
        const formData = await request.formData();
        const nzbFile = formData.get("nzbFile");
        if (nzbFile instanceof File) {
            await backendClient.addNzb(nzbFile);
        } else {
            return { error: "Error uploading nzb." }
        }
    } catch (error) {
        if (error instanceof Error) {
            return { error: error.message };
        }
        throw error;
    }
}