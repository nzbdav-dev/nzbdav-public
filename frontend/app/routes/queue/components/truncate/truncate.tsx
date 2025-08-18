import styles from "./truncate.module.css";
import type { ReactNode } from "react";

export type TruncateProps = {
    children: ReactNode
}

export function Truncate({ children }: TruncateProps) {
    return <div className={styles.truncate}>{children}</div>;
}