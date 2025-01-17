#region License
/* 
 * Copyright (C) 1999-2021 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Arch.Arm.AArch64
{
    public partial class A64Rewriter
    {
        private void RewriteSimdBinary(string simdFormat, Domain domain, Action<Expression>? setFlags = null)
        {
            var arrayLeft = MakeArrayType(instr.Operands[1], domain);
            var tmpLeft = binder.CreateTemporary(arrayLeft);
            Expression? tmpRight = null;
            if (!(instr.Operands[2] is ImmediateOperand imm))
            {
                var arrayRight = MakeArrayType(instr.Operands[2], domain);
                tmpRight = binder.CreateTemporary(arrayRight);
            }
            var arrayDst = MakeArrayType(instr.Operands[0], domain);
            var left = RewriteOp(instr.Operands[1], true);
            var right = RewriteOp(instr.Operands[2], true);
            var dst = RewriteOp(instr.Operands[0]);
            var name = GenerateSimdIntrinsicName(simdFormat, (PrimitiveType)arrayLeft.ElementType);
            m.Assign(tmpLeft, left);
            if (tmpRight != null)
            {
                m.Assign(tmpRight, right);
            }
            else
            {
                tmpRight = right;
            }
            m.Assign(dst, host.Intrinsic(name, false, arrayDst, tmpLeft, tmpRight));
            setFlags?.Invoke(dst);
        }

        private void RewriteSimdWithScalar(string simdFormat, Domain domain, Action<Expression>? setFlags = null)
        {
            var arrayLeft = MakeArrayType(instr.Operands[1], domain);
            var arrayDst = MakeArrayType(instr.Operands[0], domain);
            var tmpLeft = binder.CreateTemporary(arrayLeft);
            var left = RewriteOp(instr.Operands[1], true);
            var right = RewriteOp(instr.Operands[2], true);
            var dst = RewriteOp(instr.Operands[0]);
            var name = GenerateSimdIntrinsicName(simdFormat, (PrimitiveType)arrayLeft.ElementType);
            m.Assign(tmpLeft, left);
            m.Assign(dst, host.Intrinsic(name, false, arrayDst, tmpLeft, right));
            setFlags?.Invoke(dst);
        }

        private void RewriteSimdUnary(string simdFormat, Domain domain)
        {
            var array = MakeArrayType(instr.Operands[0], domain);
            var tmpSrc = binder.CreateTemporary(array);
            var src = RewriteOp(instr.Operands[1], true);
            var dst = RewriteOp(instr.Operands[0]);
            var name = GenerateSimdIntrinsicName(simdFormat, (PrimitiveType)array.ElementType);
            m.Assign(tmpSrc, src);
            m.Assign(dst, host.Intrinsic(name, false, array, tmpSrc));
        }

        private void RewriteSimdExpand(string simdFormat, Domain domain = Domain.None)
        {
            var arrayDst = MakeArrayType(instr.Operands[0], domain);
            var tmpSrc = binder.CreateTemporary(arrayDst);
            var src = RewriteOp(instr.Operands[1], true);
            var dst = RewriteOp(instr.Operands[0]);
            var name = GenerateSimdIntrinsicName(simdFormat, (PrimitiveType)arrayDst.ElementType);
            m.Assign(tmpSrc, src);
            m.Assign(dst, host.Intrinsic(name, false, arrayDst.ElementType, tmpSrc));
        }

        private void RewriteSimdReduce(string simdFormat, Domain domain)
        {
            var arraySrc = MakeArrayType(instr.Operands[1], domain);
            var tmpSrc = binder.CreateTemporary(arraySrc);
            var src = RewriteOp(instr.Operands[1], true);
            var dst = RewriteOp(instr.Operands[0]);
            var name = GenerateSimdIntrinsicName(simdFormat, (PrimitiveType)arraySrc.ElementType);
            m.Assign(tmpSrc, src);
            m.Assign(dst, host.Intrinsic(name, false, arraySrc.ElementType, tmpSrc));
        }

        private string GenerateSimdIntrinsicName(string simdFormat, PrimitiveType elementType)
        {
            string prefix = "i";
            if (elementType.Domain == Domain.Real)
            {
                prefix = "f";
            }
            else if (elementType.Domain == Domain.UnsignedInt)
            {
                prefix = "u";
            }
            return string.Format(simdFormat, $"{prefix}{elementType.BitSize}");
        }

        private void RewriteAddv()
        {
            RewriteSimdReduce("__sum_{0}", Domain.Integer);
        }

        private void RewriteCm(string name)
        {
            RewriteSimdBinary(name, Domain.None);
        }

        private void RewriteDup()
        {
            RewriteSimdExpand("__dup_{0}");
        }

        private void RewriteExtr()
        {
            var rHi = ((RegisterOperand) instr.Operands[1]).Register;
            var rLo = ((RegisterOperand) instr.Operands[1]).Register;
            var opDst = RewriteOp(0);
            var lsb = ((ImmediateOperand) instr.Operands[3]).Value;
            if (rHi == rLo) // ROR
            {
                var op = binder.EnsureRegister(rHi);
                m.Assign(opDst, host.Intrinsic(IntrinsicProcedure.Ror, true, opDst.DataType, op, lsb));
            }
            else
            {
                var seq = binder.EnsureSequence(PrimitiveType.Word128, rHi, rLo);
                m.Assign(opDst, m.Slice(opDst.DataType, seq, lsb.ToInt32()));
            }
        }

        private void RewriteLdN(string fnName)
        {
            var (ea,_) = RewriteEffectiveAddress((MemoryOperand)instr.Operands[1]);
            var vec = ((VectorMultipleRegisterOperand)instr.Operands[0]);
            if (vec.Index < 0)
            {
                var args = new List<Expression> { ea };
                args.AddRange(vec.GetRegisters()
                    .Select(r => (Expression)m.Out(r.DataType, binder.EnsureRegister(r))));
                m.SideEffect(host.Intrinsic(fnName, false, VoidType.Instance, args.ToArray()));
            }
            else
            {
                NotImplementedYet();
            }
        }

        private void RewriteLdNr(string fnName)
        {
            var (ea, _) = RewriteEffectiveAddress((MemoryOperand)instr.Operands[1]);
            var vec = ((VectorMultipleRegisterOperand)instr.Operands[0]);
            var args = new List<Expression> { ea };
            args.AddRange(vec.GetRegisters()
                .Select(r => (Expression)m.Out(r.DataType, binder.EnsureRegister(r))));
            m.SideEffect(host.Intrinsic(fnName, false, VoidType.Instance, args.ToArray()));
        }

        private void RewriteSmsubl()
        {
            var m1 = RewriteOp(1);
            var m2 = RewriteOp(2);
            var s = RewriteOp(3);
            var product = m.IMul(m1, m2);
            product.DataType = s.DataType;
            var dst = RewriteOp(0);
            m.Assign(dst, m.ISub(s, product));
        }

        private void RewriteStN(string fnName)
        {
            var (ea, _) = RewriteEffectiveAddress((MemoryOperand)instr.Operands[1]);
            var vec = ((VectorMultipleRegisterOperand)instr.Operands[0]);
            if (vec.Index < 0)
            {
                var args = new List<Expression> { ea };
                args.AddRange(vec.GetRegisters()
                    .Select(r => (Expression)binder.EnsureRegister(r)));
                m.SideEffect(host.Intrinsic(fnName, false, VoidType.Instance, args.ToArray()));
            }
            else
            {
                var dtElem = PrimitiveType.CreateWord(Bitsize(vec.ElementType));
                int offset = 0;
                foreach (var reg in vec.GetRegisters())
                {
                    var vReg = binder.EnsureRegister(reg);
                    var indexed = m.ARef(dtElem, vReg, Constant.Int32(vec.Index));
                    var eaOffset = offset == 0 ? ea : m.IAddS(ea, offset);
                    m.Assign(m.Mem(dtElem, eaOffset), indexed);
                    offset += dtElem.Size;
                }
            }
        }

        private void RewriteMovi()
        {
            RewriteSimdExpand("__movi_{0}");
        }

        private void RewriteScvtf()
        {
            var srcReg = ((RegisterOperand)instr.Operands[1]).Register;
            var dstReg = ((RegisterOperand)instr.Operands[0]).Register;
            var src = binder.EnsureRegister(srcReg);
            var dst = binder.EnsureRegister(dstReg);
            var realType = PrimitiveType.Create(Domain.Real, (int)dstReg.BitSize);
            if (instr.Operands.Length == 3)
            {
                // fixed point conversion.
                var fprec = RewriteOp(instr.Operands[2]);
                m.Assign(dst, host.Intrinsic("__scvtf_fixed", false, realType, src, fprec));
            }
            else if (Registers.IsIntegerRegister(srcReg))
            {
                var intType = PrimitiveType.Create(Domain.SignedInt, (int)srcReg.BitSize);
                m.Assign(dst, m.Convert(src, intType, realType));
            }
            else if (instr.VectorData == VectorData.Invalid)
            {
                var intType = PrimitiveType.Create(Domain.SignedInt, (int)srcReg.BitSize);
                m.Assign(dst, m.Convert(src, intType, realType));
            }
            else
            {
                RewriteSimdUnary("__scvtf_{0}", Domain.SignedInt);
            }
        }

        private void RewriteShrn()
        {
            RewriteSimdWithScalar("__shrn_{0}", Domain.None);
        }

        private void RewriteSmax()
        {
            RewriteSimdBinary("__smax_{0}", Domain.SignedInt);
        }

        private void RewriteSmaxv()
        {
            RewriteSimdReduce("__smax_{0}", Domain.SignedInt);
        }

        private void RewriteUabd()
        {
            if (instr.Operands[1] is VectorRegisterOperand vop)
            {
                var domain = Domain.UnsignedInt;
                var arrayLeft = MakeArrayType(instr.Operands[1], domain);
                var arrayRight = MakeArrayType(instr.Operands[2], domain);
                var arrayDst = MakeArrayType(instr.Operands[0], domain);
                var tmpLeft = binder.CreateTemporary(arrayLeft);
                var tmpRight = binder.CreateTemporary(arrayRight);
                var left = RewriteOp(instr.Operands[1], true);
                var right = RewriteOp(instr.Operands[2], true);
                var dst = RewriteOp(instr.Operands[0]);
                var name = GenerateSimdIntrinsicName("__uabd_{0}", (PrimitiveType) arrayLeft.ElementType);
                m.Assign(tmpLeft, left);
                m.Assign(tmpRight, right);
                m.Assign(dst, host.Intrinsic(name, false, arrayDst, tmpLeft, tmpRight));
            }
        }

        private void RewriteUaddw()
        {
            if (instr.VectorData != VectorData.Invalid || instr.Operands[1] is VectorRegisterOperand)
            {
                var domain = Domain.UnsignedInt;
                var arrayLeft = MakeArrayType(instr.Operands[1], domain);
                var arrayRight = MakeArrayType(instr.Operands[2], domain);
                var arrayDst = MakeArrayType(instr.Operands[0], domain);
                var tmpLeft = binder.CreateTemporary(arrayLeft);
                var tmpRight = binder.CreateTemporary(arrayRight);
                var left = RewriteOp(instr.Operands[1], true);
                var right = RewriteOp(instr.Operands[2], true);
                var dst = RewriteOp(instr.Operands[0]);
                var name = GenerateSimdIntrinsicName("__uaddw_{0}", (PrimitiveType)arrayLeft.ElementType);
                m.Assign(tmpLeft, left);
                m.Assign(tmpRight, right);
                m.Assign(dst, host.Intrinsic(name, false, arrayDst, tmpLeft, tmpRight));
            }
            else
            {
                NotImplementedYet();
            }

        }

        private void RewriteUmlal()
        {
            if (instr.VectorData != VectorData.Invalid || instr.Operands[1] is VectorRegisterOperand)
            {
                var domain = Domain.UnsignedInt;
                var arrayLeft = MakeArrayType(instr.Operands[1], domain);
                var arrayRight = MakeArrayType(instr.Operands[2], domain);
                var arrayDst = MakeArrayType(instr.Operands[0], domain);
                var tmpLeft = binder.CreateTemporary(arrayLeft);
                var tmpRight = binder.CreateTemporary(arrayRight);
                var left = RewriteOp(instr.Operands[1], true);
                var right = RewriteOp(instr.Operands[2], true);
                var dst = RewriteOp(instr.Operands[0]);
                var name = GenerateSimdIntrinsicName("__umlal_{0}", (PrimitiveType)arrayLeft.ElementType);
                m.Assign(tmpLeft, left);
                m.Assign(tmpRight, right);
                m.Assign(dst, host.Intrinsic(name, false, arrayDst, tmpLeft, tmpRight, dst));
            }
            else
            {
                NotImplementedYet();
            }
        }
    }
}
