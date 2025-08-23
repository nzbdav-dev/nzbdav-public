import React from "react"
import styles from "./breadcrumbs.module.css"
import { useCallback } from "react"
import { useNavigate } from "react-router"
import { className } from "~/utils/styling"

export type BreadcrumbProps = {
    parentDirectories: string[]
}

export function Breadcrumbs({ parentDirectories }: BreadcrumbProps): React.ReactNode {
    const navigate = useNavigate();
    const onClick = useCallback((index: number) => {
        if (index === -1) return navigate("/explore");
        navigate(`/explore/${parentDirectories.slice(0, index + 1).join('/')}`)
    }, [parentDirectories, navigate]);

    return (
        <div className={styles.container}>
            <div {...className([styles.home, styles.directory])} onClick={() => onClick(-1)}>
                <div className={styles["home-icon"]} />
                {parentDirectories.length == 0 && <div>home</div>}
            </div>
            {parentDirectories.map((parentDirectory, index) =>
                <React.Fragment key={index}>
                    <div className={styles.separator} />
                    <div className={styles.directory} onClick={() => onClick(index)}>
                        {parentDirectory}
                    </div>
                </React.Fragment>
            )}
        </div>
    );
}