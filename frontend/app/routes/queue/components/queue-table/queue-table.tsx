import type { QueueResponse, QueueSlot } from "~/clients/backend-client.server"
import styles from "./queue-table.module.css"
import tableStyles from "../page-table/page-table.module.css"
import { Badge, OverlayTrigger, Tooltip } from "react-bootstrap"
import { ActionButton } from "../action-button/action-button"
import { PageTable } from "../page-table/page-table"
import { Truncate } from "../truncate/truncate"
import { useCallback, useState } from "react"
import { ConfirmModal } from "../confirm-modal/confirm-modal"

export type QueueTableProps = {
    queue: QueueResponse
}

export function QueueTable({ queue }: QueueTableProps) {
    return (
        <PageTable responsive striped>
            <thead>
                <tr>
                    <th>Name</th>
                    <th className={tableStyles.disappear}>Category</th>
                    <th className={tableStyles.disappear}>Status</th>
                    <th className={tableStyles.disappear}>Size</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                {queue.slots.map(slot =>
                    <QueueRow slot={slot} key={slot.nzo_id} />
                )}
            </tbody>
        </PageTable>
    );
}

type QueueRowProps = {
    slot: QueueSlot
}

export function QueueRow({ slot }: QueueRowProps) {
    // state
    const [isConfirmingDelete, setIsConfirmingDelete] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [isDeleted, setIsDeleted] = useState(false);
    const className = isDeleting ? tableStyles.deleting : undefined;

    // events
    const onDelete = useCallback(() => {
        setIsConfirmingDelete(true);
    }, [setIsConfirmingDelete]);

    const onCancelDelete = useCallback(() => {
        setIsConfirmingDelete(false);
    }, [setIsConfirmingDelete]);

    const onConfirmDelete = useCallback(async () => {
        setIsConfirmingDelete(false);
        setIsDeleting(true);
        try {
            const response = await fetch(`/queue/remove-from-queue?nzo_id=${slot.nzo_id}`);
            if (response.ok) {
                const data = await response.json();
                if (data.status === true) {
                    setIsDeleted(true);
                    return;
                }
            }
        } catch { }
        setIsDeleting(false);
    }, [slot.nzo_id, setIsConfirmingDelete, setIsDeleting, setIsDeleted]);

    // view
    return isDeleted ? null : (
        <>
            <tr key={slot.nzo_id} className={className}>
                <td>
                    <Truncate>{slot.filename}</Truncate>
                    <div className={tableStyles.reappear}>
                        <StatusBadge status={slot.status} />
                        <CategoryBadge category={slot.cat} />
                        <div>{formatFileSize(Number(slot.mb) * 1024 * 1024)}</div>
                    </div>
                </td>
                <td className={tableStyles.disappear}>
                    <CategoryBadge category={slot.cat} />
                </td>
                <td className={tableStyles.disappear}>
                    <StatusBadge status={slot.status} />
                </td>
                <td className={tableStyles.disappear}>
                    {formatFileSize(Number(slot.mb) * 1024 * 1024)}
                </td>
                <td>
                    <ActionButton type="delete" disabled={isDeleting} onClick={onDelete} />
                </td>
            </tr>
            <ConfirmModal
                show={isConfirmingDelete}
                title="Remove From Queue?"
                message={slot.filename}
                onConfirm={onConfirmDelete}
                onCancel={onCancelDelete} />
        </>
    )
}

export function CategoryBadge({ category }: { category: string }) {
    const categoryLower = category?.toLowerCase();
    let variant = 'secondary';
    if (categoryLower === 'movies') variant = 'primary';
    if (categoryLower === 'tv') variant = 'info';
    return <Badge bg={variant} style={{ width: '85px' }}>{categoryLower}</Badge>
}

export function StatusBadge({ status, error }: { status: string, error?: string }) {
    const statusLower = status?.toLowerCase();
    let variant = "secondary";
    if (statusLower === "completed") variant = "success";
    if (statusLower === "failed") variant = "danger";
    if (statusLower === "downloading") variant = "primary";

    if (error?.startsWith("Article with message-id")) error = "Missing articles";
    const badgeClass = statusLower === "failed" ? styles["failure-badge"] : "";
    const overlay = statusLower == "failed"
        ? <Tooltip>{error}</Tooltip>
        : <></>;

    return (
        <OverlayTrigger placement="top" overlay={overlay} trigger="click">
            <Badge bg={variant} className={badgeClass} style={{ width: '85px' }}>{statusLower}</Badge>
        </OverlayTrigger>
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