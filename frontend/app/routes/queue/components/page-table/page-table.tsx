import { Table, type TableProps } from "react-bootstrap";
import styles from "./page-table.module.css";

export function PageTable(props: TableProps) {
    return (
        <div className={styles.container}>
            <Table className={styles["page-table"]} {...props} />
        </div>
    )
}