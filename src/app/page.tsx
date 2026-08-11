import { redirect } from "next/navigation";

/** Game is static at /index.html (synced from web-sandbox). */
export default function Page() {
  redirect("/index.html");
}
