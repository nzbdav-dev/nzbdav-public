import type { Route } from "./+types/route";
import { Layout } from "../_index/components/layout/layout";
import { TopNavigation } from "../_index/components/top-navigation/top-navigation";
import { LeftNavigation } from "../_index/components/left-navigation/left-navigation";
import styles from "./route.module.css"
import { Tabs, Tab, Button } from "react-bootstrap"
import { backendClient } from "~/clients/backend-client.server";
import { redirect } from "react-router";
import { sessionStorage } from "~/auth/authentication.server";
import { UsenetProviders } from "./usenet-providers/usenet-providers";

// Helper function to check if provider settings have been updated
function isProvidersSettingsUpdated(config: Record<string, string>, newConfig: Record<string, string>): boolean {
    // Check if provider count changed
    if (config["usenet.providers.count"] !== newConfig["usenet.providers.count"]) return true;
    if (config["usenet.providers.primary"] !== newConfig["usenet.providers.primary"]) return true;
    
    // Check all provider-specific keys
    const allKeys = Object.keys(newConfig);
    const providerKeys = allKeys.filter(key => key.startsWith("usenet.provider."));
    
    return providerKeys.some(key => config[key] !== newConfig[key]);
}
import React from "react";
import { isSabnzbdSettingsUpdated, isSabnzbdSettingsValid, SabnzbdSettings } from "./sabnzbd/sabnzbd";
import { isWebdavSettingsUpdated, isWebdavSettingsValid, WebdavSettings } from "./webdav/webdav";

const defaultConfig = {
    "api.key": "",
    "api.categories": "",
    "api.ensure-importable-video": "true",
    // Multi-provider configuration
    "usenet.providers.count": "0",
    "usenet.providers.primary": "0",
    "usenet.connections-per-stream": "",
    "webdav.user": "",
    "webdav.pass": "",
    "rclone.mount-dir": "",
}

export async function loader({ request }: Route.LoaderArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    // fetch the config items
    var configItems = await backendClient.getConfig(Object.keys(defaultConfig));

    // transform to a map
    const config: Record<string, string> = defaultConfig;
    for (const item of configItems) {
        config[item.configName] = item.configValue;
    }

    // Load provider-specific configurations dynamically
    const providerCount = parseInt(config["usenet.providers.count"] || "0");
    const providerKeys: string[] = [];
    
    for (let i = 0; i < providerCount; i++) {
        const properties = ["name", "host", "port", "use-ssl", "connections", "user", "pass", "priority", "enabled"];
        properties.forEach(prop => {
            providerKeys.push(`usenet.provider.${i}.${prop}`);
        });
    }

    if (providerKeys.length > 0) {
        const providerConfigItems = await backendClient.getConfig(providerKeys);
        for (const item of providerConfigItems) {
            config[item.configName] = item.configValue;
        }
    }

    return { config: config }
}

export default function Settings(props: Route.ComponentProps) {
    return (
        <Layout
            topNavComponent={TopNavigation}
            bodyChild={<Body config={props.loaderData.config} />}
            leftNavChild={<LeftNavigation />}
        />
    );
}

type BodyProps = {
    config: Record<string, string>
};

function Body(props: BodyProps) {
    const [config, setConfig] = React.useState(props.config);
    const [newConfig, setNewConfig] = React.useState(config);
    const [isProvidersReadyToSave, setIsProvidersReadyToSave] = React.useState(false);
    const [isSaving, setIsSaving] = React.useState(false);
    const [isSaved, setIsSaved] = React.useState(false);

    const isProvidersUpdated = isProvidersSettingsUpdated(config, newConfig);
    const isSabnzbdUpdated = isSabnzbdSettingsUpdated(config, newConfig);
    const isWebdavUpdated = isWebdavSettingsUpdated(config, newConfig);
    const isUpdated = isProvidersUpdated || isSabnzbdUpdated || isWebdavUpdated;

    const providersTitle = isProvidersUpdated ? "Usenet Providers ✏️" : "Usenet Providers";
    const sabnzbdTitle = isSabnzbdUpdated ? "SABnzbd ✏️" : "SABnzbd";
    const webdavTitle = isWebdavUpdated ? "WebDAV ✏️" : "WebDAV";

    const saveButtonLabel = isSaving ? "Saving..."
        : !isUpdated && isSaved ? "Saved ✅"
        : !isUpdated && !isSaved ? "There are no changes to save"
        : isProvidersUpdated && !isProvidersReadyToSave ? "Must configure at least one enabled provider"
        : isSabnzbdUpdated && !isSabnzbdSettingsValid(newConfig) ? "Invalid SABnzbd settings"
        : isWebdavUpdated && !isWebdavSettingsValid(newConfig) ? "Invalid WebDAV settings"
        : "Save";
    const saveButtonVariant = saveButtonLabel === "Save" ? "primary"
        : saveButtonLabel === "Saved ✅" ? "success"
        : "secondary";
    const isSaveButtonDisabled = saveButtonLabel !== "Save";

    // events
    const onClear = React.useCallback(() => {
        setNewConfig(config);
        setIsSaved(false);
    }, [config, setNewConfig]);

    const onProvidersReadyToSave = React.useCallback((isReadyToSave: boolean) => {
        setIsProvidersReadyToSave(isReadyToSave);
    }, [setIsProvidersReadyToSave]);

    const onSave = React.useCallback(async () => {
        setIsSaving(true);
        setIsSaved(false);
        const response = await fetch("/settings/update", {
            method: "POST",
            body: (() => {
                const form = new FormData();
                const changedConfig = getChangedConfig(config, newConfig);
                form.append("config", JSON.stringify(changedConfig));
                return form;
            })()
        });
        if (response.ok) {
            setConfig(newConfig);
        }
        setIsSaving(false);
        setIsSaved(true);
    }, [config, newConfig, setIsSaving, setIsSaved, setConfig]);

    return (
        <div className={styles.container}>
            <Tabs
                defaultActiveKey="providers"
                className={styles.tabs}
            >
                <Tab eventKey="providers" title={providersTitle}>
                    <UsenetProviders config={newConfig} setNewConfig={setNewConfig} onReadyToSave={onProvidersReadyToSave}/>
                </Tab>
                <Tab eventKey="sabnzbd" title={sabnzbdTitle}>
                    <SabnzbdSettings config={newConfig} setNewConfig={setNewConfig} />
                </Tab>
                <Tab eventKey="webdav" title={webdavTitle}>
                    <WebdavSettings config={newConfig} setNewConfig={setNewConfig} />
                </Tab>
            </Tabs>
            <hr />
            {isUpdated && <Button
                className={styles.button}
                variant="secondary"
                disabled={!isUpdated}
                onClick={() => onClear()}>
                Clear
            </Button>}
            <Button
                className={styles.button}
                variant={saveButtonVariant}
                disabled={isSaveButtonDisabled}
                onClick={onSave}>
                {saveButtonLabel}
            </Button>
        </div>
    );
}

function getChangedConfig(
    config: Record<string, string>,
    newConfig: Record<string, string>
): Record<string, string> {
    let changedConfig: Record<string, string> = {};
    let configKeys = Object.keys(defaultConfig);
    for (const configKey of configKeys) {
        if (config[configKey] !== newConfig[configKey]) {
            changedConfig[configKey] = newConfig[configKey];
        }
    }
    return changedConfig;
}