import styles from "./empty-history.module.css";

export function EmptyHistory() {
    return (
        <div className={styles.container}>
            <div className={styles.content}>
                <div className={styles["icon-container"]}>
                    <div className={styles["history-icon"]}></div>
                </div>
                <div className={styles["text-content"]}>
                    <h4 className={styles.title}>No download history</h4>
                    <p className={styles.description}>
                        Completed downloads will appear here
                    </p>
                </div>
            </div>
        </div>
    );
}