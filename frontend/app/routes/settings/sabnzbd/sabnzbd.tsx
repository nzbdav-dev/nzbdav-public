import { Button, Form, InputGroup } from "react-bootstrap";
import styles from "./sabnzbd.module.css"
import { useCallback, type Dispatch, type SetStateAction } from "react";
import { className } from "~/utils/styling";

type SabnzbdSettingsProps = {
    config: Record<string, string>
    setNewConfig: Dispatch<SetStateAction<Record<string, string>>>
};

export function SabnzbdSettings({ config, setNewConfig }: SabnzbdSettingsProps) {

    const onRefreshApiKey = useCallback(() => {
        setNewConfig({ ...config, "api.key": generateNewApiKey() })
    }, [setNewConfig, config]);

    return (
        <div className={styles.container}>
            <Form.Group>
                <Form.Label htmlFor="api-key-input">API Key</Form.Label>
                <InputGroup className={styles.input}>
                    <Form.Control
                        type="text"
                        id="api-key-input"
                        aria-describedby="api-key-help"
                        value={config["api.key"]}
                        readOnly />
                    <Button variant="primary" onClick={onRefreshApiKey}>
                        Refresh
                    </Button>
                </InputGroup>
                <Form.Text id="api-key-help" muted>
                    Use this API key when configuring your download client in Radarr or Sonarr.
                </Form.Text>
            </Form.Group>
            <hr />
            <Form.Group>
                <Form.Label htmlFor="categories-input">Categories</Form.Label>
                <Form.Control
                    {...className([styles.input, !isValidCategories(config["api.categories"]) && styles.error])}
                    type="text"
                    id="categories-input"
                    aria-describedby="categories-help"
                    value={config["api.categories"]}
                    placeholder="tv, movies, audio, software"
                    onChange={e => setNewConfig({ ...config, "api.categories": e.target.value })} />
                <Form.Text id="categories-help" muted>
                    Comma-separated categories. Only letters, numbers, and dashes are allowed.
                </Form.Text>
            </Form.Group>
            <hr />
            <Form.Group>
                <Form.Label htmlFor="mount-dir-input">Rclone Mount Directory</Form.Label>
                <Form.Control
                    className={styles.input}
                    type="text"
                    id="mount-dir-input"
                    aria-describedby="mount-dir-help"
                    placeholder="/mnt/nzbdav"
                    value={config["rclone.mount-dir"]}
                    onChange={e => setNewConfig({ ...config, "rclone.mount-dir": e.target.value })} />
                <Form.Text id="mount-dir-help" muted>
                    The location at which you've mounted (or will mount) the webdav root, through Rclone. This is used to tell Radarr / Sonarr where to look for completed "downloads."
                </Form.Text>
            </Form.Group>
            <hr />
            <Form.Group>
                <Form.Check
                    className={styles.input}
                    type="checkbox"
                    aria-describedby="ensure-importable-video-help"
                    label={`Fail downloads for nzbs without video content`}
                    checked={config["api.ensure-importable-video"] === "true"}
                    onChange={e => setNewConfig({ ...config, "api.ensure-importable-video": "" + e.target.checked })} />
                <Form.Text id="ensure-importable-video-help" muted>
                    Whether to mark downloads as `failed` when no single video file is found inside the nzb. This will force Radarr / Sonarr to automatically look for a new nzb.
                </Form.Text>
            </Form.Group>
        </div>
    );
}

export function isSabnzbdSettingsUpdated(config: Record<string, string>, newConfig: Record<string, string>) {
    return config["api.key"] !== newConfig["api.key"]
        || config["api.categories"] !== newConfig["api.categories"]
        || config["rclone.mount-dir"] !== newConfig["rclone.mount-dir"]
        || config["api.ensure-importable-video"] !== newConfig["api.ensure-importable-video"]
}

export function isSabnzbdSettingsValid(newConfig: Record<string, string>) {
    return isValidCategories(newConfig["api.categories"]);
}

export function generateNewApiKey(): string {
    return crypto.randomUUID().toString().replaceAll("-", "");
}

function isValidCategories(categories: string): boolean {
    if (categories === "") return true;
    var parts = categories.split(",");
    return parts.map(x => x.trim()).every(x => isAlphaNumericWithDashes(x));
}

function isAlphaNumericWithDashes(input: string): boolean {
    const regex = /^[A-Za-z0-9-]+$/;
    return regex.test(input);
}