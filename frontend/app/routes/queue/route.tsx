import { redirect } from "react-router";
import { clearQueueAction } from "./actions.server";
import type { Route } from "./+types/route";
import { sessionStorage } from "~/auth/authentication.server";
import { Layout } from "../_index/components/layout/layout";
import { TopNavigation } from "../_index/components/top-navigation/top-navigation";
import { LeftNavigation } from "../_index/components/left-navigation/left-navigation";
import styles from "./route.module.css"
import { Alert } from 'react-bootstrap';
import { backendClient, type HistoryResponse, type QueueResponse } from "~/clients/backend-client.server";
import { EmptyQueue } from "./components/empty-queue/empty-queue";
import { EmptyHistory } from "./components/empty-history/empty-history";
import { HistoryTable } from "./components/history-table/history-table";
import { QueueTable } from "./components/queue-table/queue-table";
import { useEffect } from "react";
import { useRevalidator, useNavigation } from "react-router";
import { SkeletonTable } from "~/components/skeleton-loader";

type BodyProps = {
    loaderData: { queue: QueueResponse, history: HistoryResponse },
    actionData: { error?: string, success?: boolean } | undefined
};

export async function loader({ request }: Route.LoaderArgs) {
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

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
    const revalidator = useRevalidator();

    // Auto-refresh queue data only when there are active downloads
    useEffect(() => {
        const hasActiveDownloads = props.loaderData.queue.slots.some((slot: any) => 
            slot.status.toLowerCase() === 'downloading' || 
            slot.status.toLowerCase() === 'queued'
        );

        if (!hasActiveDownloads) return;

        const interval = setInterval(() => {
            if (revalidator.state === "idle") {
                revalidator.revalidate();
            }
        }, 5000);

        return () => clearInterval(interval);
    }, [revalidator, props.loaderData.queue.slots]);

    return (
        <Layout
            topNavComponent={TopNavigation}
            bodyChild={<Body loaderData={props.loaderData} actionData={props.actionData} />}
            leftNavChild={<LeftNavigation />}
        />
    );
}

function Body({ loaderData, actionData }: BodyProps) {
    const { queue, history } = loaderData;
    const revalidator = useRevalidator();
    const navigation = useNavigation();
    const isLoading = revalidator.state === "loading" || navigation.state === "loading";

    // Revalidate immediately after successful upload
    useEffect(() => {
        if (actionData?.success) {
            revalidator.revalidate();
        }
    }, [actionData?.success, revalidator]);

    return (
        <div className={styles.container}>
            {/* queue */}
            <div className={styles.section}>
                <h3 className={styles["section-title"]}>
                    Queue
                </h3>
                <div className={styles["section-body"]}>
                    {/* messages */}
                    {actionData?.error &&
                        <Alert variant="danger">
                            {actionData.error}
                        </Alert>
                    }
                    {actionData?.success &&
                        <Alert variant="success">
                            NZB file added successfully!
                        </Alert>
                    }
                    {isLoading ? (
                        <SkeletonTable />
                    ) : (
                        queue.slots.length > 0 ? <QueueTable queue={queue} /> : <EmptyQueue />
                    )}
                </div>
            </div>

            {/* history */}
            <div className={styles.section}>
                <h3 className={styles["section-title"]}>
                    History
                </h3>
                <div className={styles["section-body"]}>
                    {isLoading ? (
                        <SkeletonTable />
                    ) : (
                        history.slots.length > 0 ? <HistoryTable history={history} /> : <EmptyHistory />
                    )}
                </div>
            </div>
        </div>
    );
}

export async function action({ request }: Route.ActionArgs) {
    const formData = await request.clone().formData();
    const intent = formData.get("intent");

    if (intent === "clear-queue") {
        return clearQueueAction({ request, params: {}, context: { VALUE_FROM_EXPRESS: '' } });
    }

    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    try {
        const formData = await request.formData();
        const nzbFile = formData.get("nzbFile");
        if (nzbFile instanceof File) {
            await backendClient.addNzb(nzbFile);
            return { success: true };
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

