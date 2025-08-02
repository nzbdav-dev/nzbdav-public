import type { QueueResponse } from "~/clients/backend-client.server"
import styles from "./queue-table.module.css"
import { Badge, Button, OverlayTrigger, ProgressBar, Table, Tooltip } from "react-bootstrap"
import { Form } from "react-router";

export type QueueTableProps = {
    queue: QueueResponse
}

export function QueueTable({ queue }: QueueTableProps) {
    return (
        <div>
            <div className={styles["table-actions"]}>
                <Form method="post">
                    <Button variant="danger" type="submit" name="intent" value="clear-queue">
                        Clear Queue
                    </Button>
                </Form>
            </div>
        <Table responsive>
            <thead>
                <tr>
                    <th className={styles["first-table-header"]}>FileName</th>
                    <th className={styles["table-header"]}>Category</th>
                    <th className={styles["table-header"]}>Progress</th>
                    <th className={styles["table-header"]}>Status</th>
                    <th className={styles["last-table-header"]}>Size</th>
                </tr>
            </thead>
            <tbody>
                {queue.slots.map((slot, index) =>
                    <tr key={index} className={styles["table-row"]}>
                        <td className={styles["row-title"]}>
                            <div className={styles.truncate}>
                                {slot.filename}
                            </div>
                        </td>
                        <td className={styles["row-column"]}>
                            <CategoryBadge category={slot.cat} />
                        </td>
                        <td className={styles["row-column"]}>
                            <QueueProgressBar percentage={slot.percentage} status={slot.status} />
                        </td>
                        <td className={styles["row-column"]}>
                            <StatusBadge status={slot.status} />
                        </td>
                        <td className={styles["row-column"]}>
                            <div className={styles["size-info"]}>
                                <div className={styles["size-total"]}>
                                    {formatFileSize(Number(slot.mb) * 1024 * 1024)}
                                </div>
                                {slot.status.toLowerCase() === 'downloading' && (
                                    <div className={styles["size-remaining"]}>
                                        {formatFileSize(Number(slot.mbleft) * 1024 * 1024)} left
                                    </div>
                                )}
                            </div>
                        </td>
                    </tr>
                )}
            </tbody>
        </Table>
    </div>
    );
}

export function CategoryBadge({ category }: { category: string }) {
    const categoryLower = category?.toLowerCase();
    let variant = 'secondary';
    if (categoryLower === 'movies') variant = 'primary';
    if (categoryLower === 'tv') variant = 'info';
    if (categoryLower === 'music') variant = 'warning';
    return (
        <Badge 
            bg={variant} 
            className={styles["category-badge"]}
        >
            {categoryLower}
        </Badge>
    );
}

export function StatusBadge({ status, error }: { status: string, error?: string }) {
    const statusLower = status?.toLowerCase();
    let variant = "secondary";
    if (statusLower === "completed") variant = "success";
    if (statusLower === "failed") variant = "danger";
    if (statusLower === "downloading") variant = "primary";
    if (statusLower === "queued") variant = "info";
    if (statusLower === "paused") variant = "warning";

    if (error?.startsWith("Article with message-id")) error = "Missing articles";
    const badgeClass = `${styles["status-badge"]} ${statusLower === "failed" ? styles["failure-badge"] : ""}`;
    const overlay = statusLower == "failed"
        ? <Tooltip>{error}</Tooltip>
        : <></>;

    return (
        <OverlayTrigger placement="top" overlay={overlay} trigger="click">
            <Badge bg={variant} className={badgeClass}>
                {statusLower}
            </Badge>
        </OverlayTrigger>
    );
}

export function QueueProgressBar({ percentage, status }: { percentage: string, status: string }) {
    const progressValue = parseFloat(percentage) || 0;
    const statusLower = status?.toLowerCase();
    
    let variant = "secondary";
    if (statusLower === "downloading") variant = "primary";
    if (statusLower === "completed") variant = "success";
    if (statusLower === "failed") variant = "danger";
    
    return (
        <div className={styles["progress-container"]}>
            <ProgressBar 
                now={progressValue} 
                variant={variant}
                className={styles["progress-bar"]}
            />
            <span className={styles["progress-text"]}>
                {progressValue.toFixed(1)}%
            </span>
        </div>
    );
}

export function formatFileSize(bytes: number) {
    var suffix = "B";
    if (bytes >= 1024) { bytes /= 1024; suffix = "KB"; }
    if (bytes >= 1024) { bytes /= 1024; suffix = "MB"; }
    if (bytes >= 1024) { bytes /= 1024; suffix = "GB"; }
    if (bytes >= 1024) { bytes /= 1024; suffix = "TB"; }
    if (bytes >= 1024) { bytes /= 1024; suffix = "PB"; }
    return `${bytes.toFixed(2)} ${suffix}`;
}