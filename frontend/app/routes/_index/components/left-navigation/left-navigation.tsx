import { Form, Link, useLocation } from "react-router";
import styles from "./left-navigation.module.css";

export type LefNavigationProps = {
}


export function LeftNavigation(props: LefNavigationProps) {
    const location = useLocation();
    
    const isActive = (path: string) => {
        return location.pathname === path || location.pathname.startsWith(path + '/');
    };

    return (
        <div className={styles.container}>
            <Link 
                className={`${styles.item} ${isActive('/queue') ? styles.active : ''}`} 
                to={"/queue"}
            >
                <div className={styles["queue-icon"]} />
                <div className={styles.title}>Queue & History</div>
            </Link>
            <Link 
                className={`${styles.item} ${isActive('/explore') ? styles.active : ''}`} 
                to={"/explore"}
            >
                <div className={styles["explore-icon"]} />
                <div className={styles.title}>Dav Explore</div>
            </Link>
            <Link 
                className={`${styles.item} ${isActive('/settings') ? styles.active : ''}`} 
                to={"/settings"}
            >
                <div className={styles["settings-icon"]} />
                <div className={styles.title}>Settings</div>
            </Link>

            <div className={styles.footer}>
                <div className={styles["footer-item"]}>
                    <Link to="https://github.com/nzbdav-dev/nzbdav" className={styles["github-link"]}>
                        github
                    </Link>
                    <div className={styles["github-icon"]} />
                </div>
                <div className={styles["footer-item"]}>
                    changelog
                </div>
                <div className={styles["footer-item"]}>
                    version: 0.2.0
                </div>
                <hr />
                <Form method="post" action="/logout">
                    <input name="confirm" value="true" type="hidden" />
                    <button className={styles.unstyled + ' ' + styles.item} type="submit">
                        <div className={styles["logout-icon"]} />
                        <div className={styles.title}>Logout</div>
                    </button>
                </Form>
            </div>
        </div>
    );
}