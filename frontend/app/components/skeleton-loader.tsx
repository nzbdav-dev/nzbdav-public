import styles from "./skeleton-loader.module.css";

export type SkeletonLoaderProps = {
    rows?: number;
    columns?: number;
    height?: string;
};

export function SkeletonLoader({ rows = 5, columns = 4, height = "20px" }: SkeletonLoaderProps) {
    return (
        <div className={styles.container}>
            {Array.from({ length: rows }).map((_, rowIndex) => (
                <div key={rowIndex} className={styles.row}>
                    {Array.from({ length: columns }).map((_, colIndex) => (
                        <div 
                            key={colIndex} 
                            className={styles.skeleton} 
                            style={{ height }}
                        />
                    ))}
                </div>
            ))}
        </div>
    );
}

export function SkeletonTable() {
    return (
        <div className={styles.table}>
            {/* Header */}
            <div className={styles.tableHeader}>
                <div className={styles.skeletonHeader} />
                <div className={styles.skeletonHeader} />
                <div className={styles.skeletonHeader} />
                <div className={styles.skeletonHeader} />
            </div>
            {/* Rows */}
            {Array.from({ length: 3 }).map((_, index) => (
                <div key={index} className={styles.tableRow}>
                    <div className={styles.skeletonCell} />
                    <div className={styles.skeletonCellSmall} />
                    <div className={styles.skeletonCellSmall} />
                    <div className={styles.skeletonCellSmall} />
                </div>
            ))}
        </div>
    );
}