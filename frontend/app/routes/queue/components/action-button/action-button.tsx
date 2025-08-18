import { Button } from "react-bootstrap";
import styles from "./action-button.module.css";

export type ActionButtonProps = {
    type: "delete" | "explore",
    disabled?: boolean,
    onClick?: () => void,
}

export function ActionButton({ type, disabled, onClick }: ActionButtonProps) {
    const variant = type === "delete" ? "outline-danger" : "outline-warning";
    return (
        <Button
            className={styles["action-button"]}
            variant={variant} onClick={onClick}
            disabled={disabled}>
            {type === "delete" && <div className={styles["trash-icon"]} />}
            {type === "explore" && <div className={styles["directory-icon"]} />}
        </Button>
    )
}