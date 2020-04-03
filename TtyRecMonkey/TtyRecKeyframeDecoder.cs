﻿// Copyright (c) 2010 Michael B. Edwin Rickert
//
// See the file LICENSE.txt for copying permission.

using Putty;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DCSSReplay
{
    public struct TtyRecFrame
    {
        public TimeSpan SinceStart;
        public TerminalCharacter[,] Data;
    }

    public class TtyRecKeyframeDecoder : IDisposable
    {
        public void Dispose()
        {
            // n.b. Resize uses this -- we may need to refactor if we need to do something permanent

            LoadCancel = true;
            LoadThread.Join();
            foreach (var ap in Packets) using (ap.RestartPosition) { }
            Packets.Clear();

            //debug.assert( !LoadThread.IsAlive ); // We assert this...
            LoadPacketBuffer.Clear(); // ... because we're not locking this.
        }

        TtyRecFrame DumpTerminal(Terminal term, TimeSpan since_start)
        {
            var h = Height;
            var w = Width;

            var frame = new TtyRecFrame()
            {
                Data = new TerminalCharacter[w, h]
                ,
                SinceStart = since_start
            };

            for (int y = 0; y < h; ++y)
            {
                var line = term.GetLine(y);
                for (int x = 0; x < w; ++x) frame.Data[x, y] = line[x];
            }

            return frame;
        }

        static int BinarySearchIndex<T>(IList<T> list, Func<T, bool> cond)
        {
            int begin = 0, end = list.Count;
            if (end == 0) return -1; // empty list

            while (end - begin >= 10)
            {
                int mid = (begin + end) / 2;

                if (cond(list[mid]))
                {
                    end = mid + 1;
                }
                else
                {
                    begin = mid + 1;
                }
            }

            if (begin > 0) //debug.assert(!cond(list[begin-1]));
                if (end < list.Count) //debug.assert(cond(list[end]));

                    for (int i = begin; i < end; ++i) if (cond(list[i])) return i;
            return -1;
        }

        static int BinarySearchIndexFrame<T>(IList<T> list, Func<T, bool> cond, int offset = 0)
        {
            int i = BinarySearchIndex(list, cond);
            if (i == -1) return list.Count - 1;
            if (i == 0) return -1;
            return i - offset;
        }

        void DumpChunksAround(TimeSpan seektarget)
        {
            var before_seek = BinarySearchIndexFrame(Packets, ap => ap.SinceStart > seektarget, 1);
            if (before_seek == -1) before_seek = 0;
            while (before_seek > 0 && Packets[before_seek].RestartPosition == null) --before_seek;

#if DEBUG
            var reference_before_seek = Packets.FindLastIndex(ap => ap.RestartPosition != null && ap.SinceStart <= seektarget);
            if (reference_before_seek == -1) reference_before_seek = 0;
            //debug.assert( before_seek == reference_before_seek );
#endif

            var after_seek = Packets.FindIndex(before_seek + 1, ap => ap.RestartPosition != null && ap.SinceStart > seektarget);
            if (after_seek == -1) after_seek = Packets.Count;

            // we now have goalposts 'before_seek' and 'after_seek' which fence our seek target
            // expand our breadth one more restart marker:

            before_seek = Packets.FindLastIndex(Math.Max(0, before_seek - 1), ap => ap.RestartPosition != null);
            if (before_seek == -1) before_seek = 0;

            if (after_seek >= Packets.Count - 1)
            {
                after_seek = Packets.Count;
            }
            else
            {
                //debug.assert( after_seek<Packets.Count-1 );
                after_seek = Packets.FindIndex(after_seek + 1, ap => ap.RestartPosition != null);
                if (after_seek == -1) after_seek = Packets.Count;
            }

            SetActiveRange(before_seek, after_seek);
        }

        int LastActiveRangeStart = int.MaxValue;
        int LastActiveRangeEnd = int.MinValue;
        void SetActiveRange(int start, int end)
        {
            //debug.assert( start<end );
            //debug.assert( Packets[start].RestartPosition != null );

            bool need_decode = false;

            // First, we strong reference everything we can, making note if we're missing anything via need_decode:
            for (int i = start; i < end; ++i)
            {
                var p = Packets[i];
                if (p.DecodedCache != null) continue;
                var weak = (p.DecodedCache == null) ? null : p.DecodedCacheWeak.Target;

                if (weak != null)
                {
                    p.DecodedCache = (TerminalCharacter[,])weak;
                }
                else
                {
                    need_decode = true;
                }
            }

            // Next, we stop strong referencing everything outside this range:
            for (int i = LastActiveRangeStart; i < start; ++i) Packets[i].DecodedCache = null;
            for (int i = end; i < LastActiveRangeEnd; ++i) Packets[i].DecodedCache = null;
            LastActiveRangeStart = start;
            LastActiveRangeEnd = end;

            if (!need_decode) return;

            // Finally, if necessary, calculate anything we're missing in the range:
            Terminal term = null;
            for (int i = start; i < end; ++i)
            {
                var p = Packets[i];
                if (p.RestartPosition != null)
                {
                    using (term) { }
                    term = new Terminal(p.RestartPosition);
                }
                if (p.DecodedCache == null)
                {
                    p.DecodedCache = DumpTerminal(term, Packets[i].SinceStart).Data;
                    // p.DecodedCacheWeak = new WeakReference( p.DecodedCache ); // TODO:  Hook up to a configuration flag?  Actually not that useful from the looks of it.
                    Packets[i] = p;
                }
                if (p.Payload != null) term.Send(p.Payload);
            }
            using (term) { }
        }

        public void Seek(TimeSpan when)
        {
            lock (LoadPacketBuffer)
            {
                while (LoadPacketBuffer.Count > 0)
                {
                    var apr = LoadPacketBuffer.Dequeue();
                    Keyframes += apr.Count(ap => ap.IsKeyframe);
                    Packets.AddRange(apr);
                }
            }
            if (Packets.Count <= 0) return;

            DumpChunksAround(when);
            var i = BinarySearchIndexFrame(Packets, ap => ap.SinceStart >= when);
            if (i == -1) i = 0;
            //debug.assert(Packets[i].DecodedCache!=null);
            CurrentFrame.SinceStart = Packets[i].SinceStart;
            CurrentFrame.Data = Packets[i].DecodedCache;
        }

        public TimeSpan Length
        {
            get
            {
                return Packets.Count > 0 ? Packets.Last().SinceStart : TimeSpan.Zero;
            }
        }

        readonly List<AnnotatedPacket> Packets = new List<AnnotatedPacket>();
        public TtyRecFrame CurrentFrame;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public TtyRecKeyframeDecoder(int w, int h, IEnumerable<Stream> streams, TimeSpan between_stream_delay)
        {
            Width = w;
            Height = h;

            LoadThread = new Thread(() => DoBackgroundLoad());
            LoadStreams = streams;
            LoadBetweenStreamDelay = between_stream_delay;
            LoadThread.Start();

            if (Packets.Count <= 0) return;
            CurrentFrame = DumpTerminal(Packets[0].RestartPosition, Packets[0].SinceStart);
        }

        public void Resize(int w, int h)
        {
            Dispose();
            Width = w;
            Height = h;
            LoadCancel = false;

            LoadThread = new Thread(() => DoBackgroundLoad());
            LoadThread.Start();
        }

        readonly Queue<IEnumerable<AnnotatedPacket>> LoadPacketBuffer = new Queue<IEnumerable<AnnotatedPacket>>();
        Thread LoadThread;
        IEnumerable<Stream> LoadStreams;
        TimeSpan LoadBetweenStreamDelay;
        volatile bool LoadCancel;

        void DoBackgroundLoad()
        {
            var decoded = TtyRecPacket.DecodePackets(LoadStreams, LoadBetweenStreamDelay, () => LoadCancel);
            var annotated = AnnotatedPacket.AnnotatePackets(Width, Height, decoded, () => LoadCancel);

            var buffer = new List<AnnotatedPacket>();

#if DEBUG
            Thread.Sleep(1000); // make sure everything can handle having 0 packets in the buffer for a bit
#endif

            foreach (var ap in annotated)
            {
                if (ap.RestartPosition != null)
                { // n.b.:  We feed entire 'keyframe' chunks at a time to avoid SetActiveRange throwing a fit and using Putty on the chunk each time
                    lock (LoadPacketBuffer) LoadPacketBuffer.Enqueue(buffer);
                    buffer = new List<AnnotatedPacket>();
                }
                buffer.Add(ap);
            }

            lock (LoadPacketBuffer) LoadPacketBuffer.Enqueue(buffer);
            buffer = null;
        }

        public int Keyframes { get; private set; }
        public int PacketCount { get { return Packets.Count; } }
    }
}
