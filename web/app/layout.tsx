import type { Metadata, Viewport } from "next";
import { ServiceWorkerRegistration } from "../components/ServiceWorkerRegistration";
import { Archivo, JetBrains_Mono } from "next/font/google";
import "./globals.css";
const archivo = Archivo({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-archivo",
  display: "swap",
});
const mono = JetBrains_Mono({
  subsets: ["latin"],
  weight: ["400", "500", "600"],
  variable: "--font-mono",
  display: "swap",
});
export const metadata: Metadata = {
  title: "Home Energy",
  description: "Household electricity and gas, priced by the half-hour.",
  applicationName: "Home Energy",
  appleWebApp: { capable: true, title: "Home Energy", statusBarStyle: "default" },
  icons: {
    icon: "/icons/icon-192.png",
    apple: "/icons/apple-touch-icon.png",
  },
};
export const viewport: Viewport = { themeColor: "#0e1214" };
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className={`${archivo.variable} ${mono.variable}`}>
      <body>
        <ServiceWorkerRegistration />
        <div className="wrap">{children}</div>
      </body>
    </html>
  );
}
