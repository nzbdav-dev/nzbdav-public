import { redirect } from "react-router";
import type { Route } from "./+types/route";
import { backendClient } from "~/clients/backend-client.server";
import { sessionStorage } from "~/auth/authentication.server";

export async function loader({ request }: Route.ActionArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    var url = new URL(request.url);
    const nzo_id = url.searchParams.get("nzo_id") || "";
    const del_completed_files = url.searchParams.get("del_completed_files") === "1";
    return await backendClient.removeFromHistory(nzo_id, del_completed_files);
}