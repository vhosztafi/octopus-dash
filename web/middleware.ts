import { NextRequest, NextResponse } from 'next/server';
import { sessionCookie, validSession } from './lib/session';

// Use the adapter's supported edge middleware path. Server Actions and BFF
// routes independently enforce the same session checks.
export async function middleware(request: NextRequest) {
  const publicPaths = new Set([
    '/login', '/manifest.webmanifest', '/sw.js', '/offline.html',
    '/icons/icon-192.png', '/icons/icon-512.png', '/icons/maskable-512.png',
    '/icons/apple-touch-icon.png',
  ]);
  if (publicPaths.has(request.nextUrl.pathname)) return NextResponse.next();
  if (request.nextUrl.pathname === '/.well-known/appspecific/com.tesla.3p.public-key.pem') return NextResponse.next();
  if (!await validSession(request.cookies.get(sessionCookie)?.value)) return NextResponse.redirect(new URL('/login', request.url));
  return NextResponse.next();
}
export const config = { matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'] };
