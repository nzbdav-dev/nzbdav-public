import { redirect } from "react-router";
import type { Route } from "./+types/route";
import { sessionStorage } from "~/auth/authentication.server";
import styles from "./route.module.css"
import { Alert } from 'react-bootstrap';
import { backendClient, type HistoryResponse, type QueueResponse } from "~/clients/backend-client.server";
import { EmptyQueue } from "./components/empty-queue/empty-queue";
import { HistoryTable } from "./components/history-table/history-table";
import { QueueTable } from "./components/queue-table/queue-table";
import { Form } from "react-router";
import { Button, ButtonGroup } from "react-bootstrap";


type BodyProps = {
    loaderData: { queue: QueueResponse, history: HistoryResponse },
    actionData: { error: string } | undefined
};

export async function loader({ request }: Route.LoaderArgs) {
    var queuePromise = backendClient.getQueue();
    var historyPromise = backendClient.getHistory();
    var queue = await queuePromise;
    var history = await historyPromise;
    return {
        queue: queue,
        history: history,
    }
}

export default function Queue(props: Route.ComponentProps) {
    return (
        <Body loaderData={props.loaderData} actionData={props.actionData} />
    );
}

function Body({ loaderData, actionData }: BodyProps) {
    const { queue, history } = loaderData;
    return (
        <div className={styles.container}>
            {/* queue */}
            <div className={styles.section}>
                <h3 className={styles["section-title"]}>
                    Queue
                </h3>
                <div className={styles["section-body"]}>
                    {/* error message */}
                    {actionData?.error &&
                        <Alert variant="danger">
                            {actionData?.error}
                        </Alert>
                    }
                    {/* Bulk-clear controls */}
                    <Form method="post" className="mb-3" onSubmit={(e) => {
                        const target = e.nativeEvent.submitter as HTMLButtonElement | null;
                        const action = target?.value ?? "";
                        const label = action === "all" ? "all items"
                                                : action === "tv" ? "all TV items"
                                                : action === "movies" ? "all Movies items"
                                                : "items";
                        if (!window.confirm(`Are you sure you want to clear ${label} from the queue?`)) {
                            e.preventDefault();
                        }
                    }}>
                        <input type="hidden" name="__intent" value="bulk-clear" />
                        <ButtonGroup>
                            <Button variant="outline-danger" type="submit" name="clear" value="all">
                                Clear All
                            </Button>
                            <Button variant="outline-warning" type="submit" name="clear" value="tv">
                                Clear TV
                            </Button>
                            <Button variant="outline-primary" type="submit" name="clear" value="movies">
                                Clear Movies
                            </Button>
                        </ButtonGroup>
                    </Form>
                    {queue.slots.length > 0 ? <QueueTable queue={queue} /> : <EmptyQueue />}
                </div>
            </div>

            {/* history */}
            <div className={styles.section}>
                <h3 className={styles["section-title"]}>
                    History
                </h3>
                <div className={styles["section-body"]}>
                    <HistoryTable history={history} />
                </div>
            </div>
        </div>
    );
}

export async function action({ request }: Route.ActionArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    const formData = await request.formData();
    const intent = formData.get("__intent");

    // ---- Bulk-clear queue (Clear All / Clear TV / Clear Movies) ----
    if (intent === "bulk-clear") {
        const clear = (formData.get("clear") || "").toString().toLowerCase();

        if (clear === "all" || clear === "tv" || clear === "movies") {
            try {
                // get the current queue
                const queue = await backendClient.getQueue();

                // defensive checks
                const slots = Array.isArray(queue?.slots) ? queue.slots : [];

                // Filter by category if needed
                const wanted = clear === "all"
                    ? slots
                    : slots.filter(s => (s?.cat || "").toString().toLowerCase() === (clear === "tv" ? "tv" : "movies"));

                                // Collect wanted IDs once
                                                const nzoIds = wanted
                                                    .map(s => s?.nzo_id ?? (s as any)?.nzoId ?? (s as any)?.id)
                                                    .filter(Boolean) as string[];

                                                if (nzoIds.length === 0) return redirect("/queue");

                                // Single round-trip to backend; backend does one DB transaction
                                try {
                                    await backendClient.removeFromQueueBulk(nzoIds);
                                } catch (err) {
                                    console.error("bulk removeFromQueue failed", err);
                                }

                // Redirect back to queue so loader refreshes the list
                return redirect("/queue");
            } catch (error) {
                return { error: "Error clearing queue items." };
            }
        }
    }

    // ---- Handle NZB file upload ----
    try {
        const nzbFile = formData.get("nzbFile");
        if (nzbFile instanceof File) {
            await backendClient.addNzb(nzbFile);
            return redirect("/queue");
        } else {
            return { error: "Error uploading nzb." };
        }
    } catch (error) {
        if (error instanceof Error) {
            return { error: error.message };
        }
        throw error;
    }
}