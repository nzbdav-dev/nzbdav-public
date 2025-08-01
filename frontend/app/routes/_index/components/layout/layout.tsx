import { useState, useEffect } from "react";
import styles from "./layout.module.css";

export type LayoutProps = {
    topNavComponent: (props: RequiredTopNavProps) => React.ReactNode,
    leftNavChild: React.ReactNode,
    bodyChild: React.ReactNode,
}

export type RequiredTopNavProps = {
    isHamburgerMenuOpen: boolean,
    onHamburgerMenuClick: () => void,
}

export function Layout(props: LayoutProps) {
    // data
    const [isHamburgerMenuOpen, setIsHamburgerMenuOpen] = useState(false);
    const [isPageVisible, setIsPageVisible] = useState(false);

    // Page transition effect
    useEffect(() => {
        setIsPageVisible(true);
    }, []);

    // events
    const onHamburgerMenuClick = function () {
        setIsHamburgerMenuOpen(!isHamburgerMenuOpen)
    }

    const onBodyClick = function() {
        setIsHamburgerMenuOpen(false);
    }

    let containerClassName = styles["container"];
    if (isHamburgerMenuOpen) containerClassName += " " + styles["hamburger-open"];

    return (
        <>
            <div className={containerClassName}>
                <div className={styles["top-navigation"]}>
                    <props.topNavComponent
                        isHamburgerMenuOpen={isHamburgerMenuOpen}
                        onHamburgerMenuClick={onHamburgerMenuClick} />
                </div>
                <div className={styles["page"]}>
                    <div className={styles["left-navigation"]}>
                        {props.leftNavChild}
                    </div>
                    <div 
                        className={`${styles["body"]} ${isPageVisible ? 'page-transition-enter-active' : 'page-transition-enter'}`} 
                        onClick={onBodyClick}
                    >
                        {props.bodyChild}
                    </div>
                </div>
            </div>
        </>
    );
}