// LocalStackVariables_text.c
// Generated by decompiling LocalStackVariables.exe
// using Reko decompiler version 0.9.3.0.

#include "LocalStackVariables.h"

// 00401000: Register int32 main(Stack int32 argc, Stack (ptr32 (ptr32 char)) argv)
int32 main(int32 argc, char ** argv)
{
	// s1.i = 0
	// s1.d = 1.0
	// s2.i = 10
	// s2.d = 11.0
	// res = GetMin(&s1, &s2)
	// s1.i = 100
	// res->i = 5
	real64 rLoc1_n = g_r4020F8;
	struct Eq_n * eax_n = GetMin(fp - 44, fp - 0x1C);
	eax_n->dw0000 = 0x05;
	// res->d = 5.0
	eax_n->r0008 = g_r4020F0;
	// printf("%d %f %d %f\n", s1.i, s1.d, s2.i, s2.d)
	printf("%d %f %d %f\n", 100, 1.0, 0x0A, rLoc1_n);
	// gbl_s = &s2
	g_ptr403018 = fp - 0x1C;
	// s2.i = 2
	// s2.d = 2.0
	// gbl_s->i = 3
	real64 rLoc1_n = g_r4020E8;
	g_ptr403018->dw0000 = 0x03;
	// gbl_s->d = 3.0
	g_ptr403018->r0008 = g_r4020E0;
	// printf("%d %f\n", s2.i, s2.d)
	printf("%d %f\n", 0x02, rLoc1_n);
	return 0x00;
}

// 004010D0: Register (ptr32 Eq_n) GetMin(Stack (ptr32 Eq_n) dwArg04, Stack (ptr32 Eq_n) dwArg08)
// Called from:
//      main
struct Eq_n * GetMin(struct Eq_n * dwArg04, struct Eq_n * dwArg08)
{
	struct Eq_n * eax_n;
	if (dwArg04->dw0000 < dwArg08->dw0000)
		eax_n = dwArg04;
	else if (dwArg04->dw0000 < dwArg08->dw0000)
		eax_n = dwArg08;
	else if (dwArg08->r0008 > dwArg04->r0008)
		eax_n = dwArg04;
	else
		eax_n = dwArg08;
	return eax_n;
}
