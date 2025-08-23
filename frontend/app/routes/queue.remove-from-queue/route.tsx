import { redirect } from "react-router";
import type { Route } from "./+types/route";
import { backendClient } from "~/clients/backend-client.server";
import { sessionStorage } from "~/auth/authentication.server";

export async function loader({ request }: Route.ActionArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    const nzo_id = (new URL(request.url)).searchParams.get("nzo_id") || "";
    return await backendClient.removeFromQueue(nzo_id);
}