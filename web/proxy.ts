import { NextRequest, NextResponse } from 'next/server';
import { sessionCookie, validSession } from './lib/session';

export async function proxy(request: NextRequest) {
  if (request.nextUrl.pathname === '/login') return NextResponse.next();
  if (!await validSession(request.cookies.get(sessionCookie)?.value)) return NextResponse.redirect(new URL('/login', request.url));
  return NextResponse.next();
}
export const config = { matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'] };
