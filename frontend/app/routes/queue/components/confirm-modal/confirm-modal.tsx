import { Button, Form, Modal } from "react-bootstrap";
import { WordWrap } from "../word-wrap/word-wrap";
import { useState } from "react";

export type ConfirmModalProps = {
    show: boolean,
    title: string,
    message: string,
    checkboxMessage?: string,
    cancelText?: string,
    confirmText?: string,
    onCancel: () => void,
    onConfirm: (isCheckboxChecked?: boolean) => void,
}

export function ConfirmModal(props: ConfirmModalProps) {
    const [isCheckboxChecked, setIsCheckboxChecked] = useState(false);

    return (
        <Modal show={props.show} onHide={props.onCancel} centered scrollable>
            <Modal.Header closeButton>
                <Modal.Title>{props.title}</Modal.Title>
            </Modal.Header>
            <Modal.Body>
                <div>
                    <div style={{ fontSize: "12px" }}>
                        <WordWrap>{props.message}</WordWrap>
                    </div>
                    {props.checkboxMessage &&
                        <Form.Check
                            type="checkbox"
                            id="modal-checkbox"
                            style={{ marginTop: '12px' }}
                            label={props.checkboxMessage}
                            checked={isCheckboxChecked}
                            onChange={(e) => setIsCheckboxChecked(Boolean(e.target.checked))} />
                    }
                </div>
            </Modal.Body>
            <Modal.Footer>
                <Button variant="secondary" onClick={props.onCancel}>
                    {props.cancelText || "Close"}
                </Button>
                <Button variant="danger" onClick={() => props.onConfirm(isCheckboxChecked)}>
                    {props.confirmText || "Confirm Removal"}
                </Button>
            </Modal.Footer>
        </Modal>
    );
}