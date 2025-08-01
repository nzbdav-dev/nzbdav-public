import { useCallback, useRef } from "react";
import styles from "./empty-queue.module.css"
import { useDropzone, type FileWithPath } from 'react-dropzone'
import { className } from "~/utils/styling";
import { useFetcher } from "react-router";

export function EmptyQueue() {
    const fetcher = useFetcher();
    const formRef = useRef<HTMLFormElement>(null);
    const inputRef = useRef<HTMLInputElement>(null);
    const isSubmitting = (fetcher.state === 'submitting');

    const { getRootProps, getInputProps, isDragActive } = useDropzone({
        accept: { 'application/x-nzb': ['.nzb'] },
        onDrop: useCallback((acceptedFiles: FileWithPath[]) => {
            const dataTransfer = new DataTransfer();
            acceptedFiles.forEach((file) => {
                const newFile = new File([file], file.name, {
                    type: 'application/x-nzb',
                    lastModified: file.lastModified,
                });
                dataTransfer.items.add(newFile);
            });
            if (inputRef?.current) {
                inputRef.current.files = dataTransfer.files;
                fetcher.submit(formRef.current);
            }
        }, [])
    });

    return (
        <fetcher.Form ref={formRef} method="POST" encType="multipart/form-data">
            <div {...className([styles.container, isDragActive && styles["drag-active"]])}  {...getRootProps()}>
                <input {...getInputProps()} />
                <input ref={inputRef} name="nzbFile" type="file" style={{ display: 'none' }} />

                {isSubmitting && (
                    <div className={styles["content"]}>
                        <div className={styles["icon-container"]}>
                            <div className={styles["loading-spinner"]}></div>
                        </div>
                        <div className={styles["text-content"]}>
                            <h4 className={styles["title"]}>Uploading...</h4>
                            <p className={styles["description"]}>
                                Adding your NZB files to the queue
                            </p>
                        </div>
                    </div>
                )}

                {/* default view */}
                {!isSubmitting && !isDragActive && <>
                    <div className={styles["content"]}>
                        <div className={styles["icon-container"]}>
                            <div className={styles["upload-icon"]}></div>
                        </div>
                        <div className={styles["text-content"]}>
                            <h4 className={styles["title"]}>No items in queue</h4>
                            <p className={styles["description"]}>
                                Drag & drop your NZB files here or click to browse
                            </p>
                        </div>
                        <button type="button" className={styles["browse-button"]}>
                            Browse Files
                        </button>
                    </div>
                </>}

                {/* when dragging a file */}
                {!isSubmitting && isDragActive && <>
                    <div className={styles["content"]}>
                        <div className={styles["icon-container"]}>
                            <div className={styles["drop-icon"]}></div>
                        </div>
                        <div className={styles["text-content"]}>
                            <h4 className={styles["title"]}>Drop to upload</h4>
                            <p className={styles["description"]}>
                                Release to add your NZB files to the queue
                            </p>
                        </div>
                    </div>
                </>}
            </div>
        </fetcher.Form>
    );
}