import styles from "./loading.module.css";

export type LoadingProps = {
    className?: string
}

export function Loading({ className }: LoadingProps) {
    return (
        <div className={`${styles.container} ${className ? className : ''}`}>
            <div className={styles["loader-ring"]}></div>
            <div className={styles["loading-text"]}>Loading...</div>
        </div>
    );
}