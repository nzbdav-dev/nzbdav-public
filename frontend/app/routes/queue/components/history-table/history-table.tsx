import type { HistoryResponse } from "~/clients/backend-client.server"
import styles from "./history-table.module.css"
import { Table } from "react-bootstrap"
import { CategoryBadge, formatFileSize, StatusBadge } from "../queue-table/queue-table"

export type HistoryTableProps = {
    history: HistoryResponse
}

export function HistoryTable({ history }: HistoryTableProps) {

    return (
        <Table responsive>
            <thead>
                <tr>
                    <th className={styles["first-table-header"]}>Name</th>
                    <th className={styles["table-header"]}>Category</th>
                    <th className={styles["table-header"]}>Status</th>
                    <th className={styles["last-table-header"]}>Size</th>
                </tr>
            </thead>
            <tbody>
                {history.slots.map((slot, index) =>
                    <tr key={index} className={styles["table-row"]}>
                        <td className={styles["row-title"]}>
                            <div className={styles.truncate}>
                                {slot.name}
                            </div>
                        </td>
                        <td className={styles["row-column"]}>
                            <CategoryBadge category={slot.category} />
                        </td>
                        <td className={styles["row-column"]}>
                            <StatusBadge status={slot.status} error={slot.fail_message} />
                        </td>
                        <td className={styles["row-column"]}>
                            {formatFileSize(slot.bytes)}
                        </td>
                    </tr>
                )}
            </tbody>
        </Table>
    );
}