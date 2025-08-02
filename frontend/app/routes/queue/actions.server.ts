import { redirect } from "react-router";
import { sessionStorage } from "~/auth/authentication.server";
import { backendClient } from "~/clients/backend-client.server";
import type { Route } from "./+types/route";

export async function clearQueueAction({ request }: Route.ActionArgs) {
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    try {
        await backendClient.clearQueue();
        return { success: true };
    } catch (error) {
        if (error instanceof Error) {
            return { error: error.message };
        }
        throw error;
    }
}