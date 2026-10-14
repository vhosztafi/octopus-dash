import { NextRequest, NextResponse } from 'next/server';
import { sessionCookie, validSession } from './lib/session';

// Use the adapter's supported edge middleware path. Server Actions and BFF
// routes independently enforce the same session checks.
export async function middleware(request: NextRequest) {
  if (request.nextUrl.pathname === '/login') return NextResponse.next();
  if (request.nextUrl.pathname === '/.well-known/appspecific/com.tesla.3p.public-key.pem') return NextResponse.next();
  if (!await validSession(request.cookies.get(sessionCookie)?.value)) return NextResponse.redirect(new URL('/login', request.url));
  return NextResponse.next();
}
export const config = { matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'] };
