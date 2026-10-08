; Windows x64 UCineCameraComponent::GetCameraView dispatch wrapper.
; Only a validated vtable DATA pointer is replaced. Game instructions stay intact.
; The wrapper updates cinematic overscan before tail-calling the original view.
; RCX = camera, XMM1 = delta time, R8 = output view. All arguments are preserved.
push rbx
sub rsp, 0x70
mov rbx, rcx
mov [rsp+0x28], r8
mov [rsp+0x30], rdx
mov [rsp+0x38], r9
movdqu [rsp+0x40], xmm1
mov r10, 0x1122334455667788
lock inc qword ptr [r10+64]
cmp dword ptr [r10], 0
je forward
mov rax, [r10+48]
cmp [rbx], rax
jne forward
test dword ptr [rbx+8], 0x30
jne forward
; Serialized MovieScene templates must stay authored. Widen runtime views,
; not templates which a later spawned camera could inherit and widen again.
mov rax, [rbx+32]
test rax, rax
jz forward
mov rax, [rax+32]
test rax, rax
jz forward
mov rdx, [r10+184]
cmp [rax], rdx
je forward

; Resolve the camera's current weak object identity.
mov rax, [r10+8]
mov r11, [rax]
xor r11, [r10+16]
test r11, r11
jz forward
mov ecx, [rbx+12]
cmp ecx, [r11+36]
jae forward
mov [rsp+0x6c], ecx
mov edx, ecx
shr edx, 16
mov rax, [r11+16]
test rax, rax
jz forward
mov rax, [rax+rdx*8]
and ecx, 65535
imul rcx, rcx, 24
add rax, rcx
cmp [rax], rbx
jne forward
test dword ptr [rax+8], 0x10200000
jne forward
mov edx, [rax+16]
mov [rsp+0x68], edx

; Locate an ownership entry, reclaiming only dead/recycled object identities.
mov eax, [rsp+0x6c]
and eax, [r10+152]
shl rax, 5
mov r8, [r10+144]
add r8, rax
mov r9d, [r10+156]
find_entry:
mov rax, [r8]
test rax, rax
jz new_entry
cmp rax, rbx
jne check_retired
mov eax, [rsp+0x68]
cmp eax, [r8+12]
je existing_entry
check_retired:
mov ecx, [r8+8]
cmp ecx, [r11+36]
jae new_entry
mov edx, ecx
shr edx, 16
mov rax, [r11+16]
mov rax, [rax+rdx*8]
and ecx, 65535
imul rcx, rcx, 24
add rax, rcx
mov ecx, [r8+12]
cmp [rax+16], ecx
jne new_entry
mov rcx, [r8]
cmp [rax], rcx
jne new_entry
test dword ptr [rax+8], 0x10200000
jne new_entry
add r8, 32
mov rax, [r10+144]
mov ecx, [r10+156]
shl rcx, 5
add rax, rcx
cmp r8, rax
jb probe_count
mov r8, [r10+144]
probe_count:
dec r9d
jnz find_entry
lock inc qword ptr [r10+136]
jmp forward

new_entry:
; No camera writes yet. Save its authored values and identity.
movss xmm0, [rbx+0x2bc]
xorps xmm4, xmm4
comiss xmm0, xmm4
jp forward
jb forward
comiss xmm0, [r10+124]
ja forward
movzx eax, word ptr [rbx+0x2d0]
test eax, 0xfefe
jnz forward
mov qword ptr [r8], 0
mov [r8+24], ax
movss [r8+16], xmm0
movss [r8+20], xmm0
mov eax, [rsp+0x6c]
mov [r8+8], eax
mov eax, [rsp+0x68]
mov [r8+12], eax
mov dword ptr [r8+28], 0
mov [r8], rbx
jmp entry_ready
existing_entry:
movss xmm0, [rbx+0x2bc]
xorps xmm4, xmm4
comiss xmm0, xmm4
jp forward
jb forward
comiss xmm0, [r10+124]
ja forward
movss xmm1, [r8+20]
subss xmm1, xmm0
andps xmm1, [r10+160]
comiss xmm1, [r10+176]
jbe entry_ready
; A new authored overscan supersedes our saved baseline.
movss [r8+16], xmm0
entry_ready:
mov [rsp+0x60], r8

; Validate the manager identity before dereferencing it.
mov ecx, [r10+32]
cmp ecx, [r11+36]
jae restore_owned
mov edx, ecx
shr edx, 16
mov rax, [r11+16]
mov rax, [rax+rdx*8]
and ecx, 65535
imul rcx, rcx, 24
add rax, rcx
mov ecx, [r10+36]
cmp [rax+16], ecx
jne restore_owned
test dword ptr [rax+8], 0x10200000
jne restore_owned
mov rcx, [r10+24]
cmp [rax], rcx
jne restore_owned
mov rdx, [r10+40]
cmp [rcx], rdx
jne restore_owned
movss xmm2, [rcx+0x3a04]
comiss xmm2, [r10+96]
jbe restore_owned
comiss xmm2, [r10+120]
ja restore_owned
; Gameplay actors do not use this exact cinematic actor type.
mov rax, [rbx+32]
test rax, rax
jz restore_owned
mov rdx, [r10+56]
cmp [rax], rdx
jne restore_owned

; Use the final crop, or recompute the uncropped sensor aspect just as
; the original view function does. Avoid its transient cached aspect.
movss xmm0, [rbx+0xd38]
comiss xmm0, [r10+108]
jbe sensor_aspect
comiss xmm0, [r10+104]
jb aspect_ready
sensor_aspect:
movss xmm0, [rbx+0xca4]
mulss xmm0, [rbx+0xccc]
divss xmm0, [rbx+0xca8]
aspect_ready:
comiss xmm0, [r10+108]
jbe restore_owned
comiss xmm0, [r10+104]
jae restore_owned
divss xmm2, xmm0
maxss xmm2, [r10+96]

; Keep the existing 35 mm reference-lens policy.
movss xmm3, [r10+4]
movss xmm4, [rbx+0xca4]
xorps xmm0, xmm0
comiss xmm4, xmm0
jbe lens_ready
comiss xmm4, [r10+116]
ja lens_ready
movss xmm5, [rbx+0xd3c]
; Match the focal-length clamp performed by the original view function.
movss xmm0, [rbx+0xcbc]
maxss xmm0, [rbx+0xcb8]
minss xmm5, xmm0
maxss xmm5, [rbx+0xcb8]
xorps xmm0, xmm0
comiss xmm5, xmm0
jbe lens_ready
comiss xmm5, [r10+112]
ja lens_ready
mulss xmm3, [r10+100]
mulss xmm3, xmm5
divss xmm3, xmm4
minss xmm3, [r10+4]
maxss xmm3, [r10+96]
lens_ready:
movss xmm0, [r8+16]
addss xmm0, [r10+96]
mulss xmm0, xmm2
mulss xmm0, xmm3
subss xmm0, [r10+96]
xorps xmm4, xmm4
comiss xmm0, xmm4
jp restore_owned
jb restore_owned
comiss xmm0, [r10+124]
ja restore_owned
movzx eax, word ptr [rbx+0x2d0]
test eax, 0xfefe
jnz restore_owned
movss [rbx+0x2bc], xmm0
mov word ptr [rbx+0x2d0], 0
movss [r8+20], xmm0
inc dword ptr [r8+28]
lock inc qword ptr [r10+72]
mov [r10+80], rbx
movss [r10+88], xmm3
movss [r10+92], xmm0
movss xmm4, [r8+16]
movss [r10+128], xmm4
jmp forward

restore_owned:
; Off mode restores authored data before the original view is built.
movss xmm0, [rbx+0x2bc]
movss xmm1, [r8+20]
subss xmm1, xmm0
andps xmm1, [r10+160]
comiss xmm1, [r10+176]
 jp forward
ja forward
movss xmm0, [r8+16]
movss [rbx+0x2bc], xmm0
movss [r8+20], xmm0
cmp word ptr [rbx+0x2d0], 0
jne forward
movzx eax, word ptr [r8+24]
mov [rbx+0x2d0], ax

forward:
lock dec qword ptr [r10+64]
mov rcx, rbx
mov r8, [rsp+0x28]
mov rdx, [rsp+0x30]
mov r9, [rsp+0x38]
movdqu xmm1, [rsp+0x40]
add rsp, 0x70
pop rbx
mov rax, 0x2233445566778899
jmp rax
