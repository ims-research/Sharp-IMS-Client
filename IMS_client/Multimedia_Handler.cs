using System;
using System.Windows;
using Gst;
using Gst.GLib;

namespace IMS_client
{
    public class GstMessageEventArgs : EventArgs
    {
        public string Type;
        public string Message;

        public GstMessageEventArgs(string type, string message)
        {
            Type = type;
            Message = message;
        }
    }

    class MultimediaHandler
    {
        public event EventHandler<GstMessageEventArgs> GstLogEvent;
        readonly Preferences _settings;

        Element _videoTxPipeline, _videoRxPipeline, _audioTxPipeline, _audioRxPipeline;
        MainLoop _loop;
        public VideoWindow VideoWindow;

        public MultimediaHandler(Preferences settings)
        {
            _settings = settings;
            Environment.SetEnvironmentVariable("GST_DEBUG", "*:2");
            //C:\gstreamer-newold\lib\gstreamer-0.10
            //System.Environment.SetEnvironmentVariable("GST_PLUGIN_PATH",  "c:\\gstreamer-newold\\lib\\gstreamer-0.10");
            //System.Environment.SetEnvironmentVariable("PATH", "c:\\gstreamer-newold\\bin;" + System.Environment.GetEnvironmentVariable("PATH"));

            // DISABLED MEDIA WHILE TESTING

            //string[] args2 = { "--gst-debug-level=*:2" };

            //Gst.Application.Init("Sharp_Client", ref args2);
            ////Gst.Application.Init();
            //System.Threading.Thread glib_loop_thread;

            //glib_loop_thread = new System.Threading.Thread(this.Glib_Loop);
            //glib_loop_thread.Start();

            VideoWindow = new VideoWindow();
        }


        public void GlibLoop()
        {
            _loop = new MainLoop();
            _loop.Run();
        }

        public void StartVideoRx(int recvPort,string name)
        {
            _videoRxPipeline = new Pipeline("videorx-pipeline");
            Bin bin = (Bin)_videoRxPipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            _videoRxPipeline.Bus.AddSignalWatch();
            _videoRxPipeline.Bus.Message += (o, args) => BusCall(args.Message);

            Element udpSrc = ElementFactory.Make("udpsrc", "udp_src");
            Element rtpH263Depayloader = ElementFactory.Make("rtph263pdepay", "h263_deplayloader");
            Element h263Decoder = ElementFactory.Make("ffdec_h263", "h263_decoder");
            Element cspFilter = ElementFactory.Make("ffmpegcolorspace", "csp_filter_rx");

            Element screenSink = ElementFactory.Make("dshowvideosink", "video-sink_rx");

            if ((_videoRxPipeline == null) || (udpSrc == null) || (rtpH263Depayloader == null) || (h263Decoder == null) || (cspFilter == null) || (screenSink == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for Recieving Video!");
            }
            else
            {
                udpSrc["port"] = recvPort;

                bin.Add(udpSrc, rtpH263Depayloader, h263Decoder, cspFilter, screenSink);
                Caps caps = Caps.FromString("application/x-rtp,clock-rate=90000,payload=96,encoding-name=H263-1998");

                if (!udpSrc.LinkFiltered(rtpH263Depayloader, caps))
                {
                    Console.WriteLine("link failed between udp_src and rtp_h263_depayloader");
                }

                if (!Element.Link(rtpH263Depayloader, h263Decoder, cspFilter, screenSink))
                {
                    Console.WriteLine("link failed between rtp_h263_depayloader and screen_sink");
                }

                Gst.Interfaces.XOverlayAdapter xadapter = new Gst.Interfaces.XOverlayAdapter(screenSink.Handle);

                VideoWindow.wfhost2.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(
                        delegate
                            {
                                VideoWindow.remote_video_label.Content = "Remote Video:" + name;
                                xadapter.XwindowId = (ulong) VideoWindow.video_rx_canvas.Handle;
                            }));

                _videoRxPipeline.SetState(State.Playing);
            }
        }

        public void StartVideoTx(string destIP, int destPort)
        {
            VideoWindow.Dispatcher.Invoke(
                   System.Windows.Threading.DispatcherPriority.Normal,
                   new Action(
                       () => VideoWindow.Show()));

            Element cameraSrc;

            _videoTxPipeline = new Pipeline("videotx-pipeline");
            Bin bin = (Bin)_videoTxPipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            _videoTxPipeline.Bus.AddSignalWatch();
            _videoTxPipeline.Bus.Message += (o, args) => BusCall(args.Message);


            if ((cameraSrc = ElementFactory.Make("videotestsrc", "video_src_tx")) == null)
            {
                Console.WriteLine("Could not create webcam-source");
            }

            Element cspFilter = ElementFactory.Make("ffmpegcolorspace", "filter_tx");
            Element cspFilter2 = ElementFactory.Make("ffmpegcolorspace", "filter2_tx");
            Element screenSink = ElementFactory.Make("dshowvideosink", "video-sink_tx");
            Element tee = ElementFactory.Make("tee", "tee_tx");

            Element screenQueue = ElementFactory.Make("queue", "screen-queue_tx");
            Element udpQueue = ElementFactory.Make("queue", "udp-queue_tx");

            Element h263Encoder = ElementFactory.Make("ffenc_h263p", "ffenc_h263p_tx");
            Element rtpH263Payloader = ElementFactory.Make("rtph263ppay", "rtp_payloader_tx");
            Element udpSink = ElementFactory.Make("udpsink", "udp_sink_tx");

            if ((_videoTxPipeline == null) || (cameraSrc == null) || (screenSink == null) || (cspFilter == null) || (cspFilter2 == null) || (h263Encoder == null) ||
                (rtpH263Payloader == null) || (udpSink == null) || (udpQueue == null) || (screenQueue == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for sending video!");
            }
            else
            {
                
            udpSink["host"] = destIP;
            udpSink["port"] = destPort;

            bin.Add(cameraSrc, screenSink, cspFilter, cspFilter2, tee, h263Encoder, rtpH263Payloader, udpSink, udpQueue, screenQueue);
            Caps caps = Caps.FromString("video/x-raw-rgb,width=" + _settings.videocall_width + ",height=" + _settings.videocall_height);

            if (!cameraSrc.LinkFiltered(tee, caps))
            {
                Console.WriteLine("link failed between camera_src and tee");
            }

            if (!Element.Link(tee, cspFilter, screenQueue, screenSink))
            {
                Console.WriteLine("link failed between tee and screen_sink");
            }

            if (!Element.Link(tee, cspFilter2, udpQueue, h263Encoder, rtpH263Payloader, udpSink))
            {
                Console.WriteLine("link failed between tee and udp_sink");
            }

            Gst.Interfaces.XOverlayAdapter xadapter = new Gst.Interfaces.XOverlayAdapter(screenSink.Handle);
            
            VideoWindow.wfhost1.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(
                        delegate
                            {
                            VideoWindow.local_video_label.Content = "Local Video: " + _settings.ims_public_user_identity;
                            xadapter.XwindowId = (ulong)VideoWindow.video_tx_canvas.Handle;
                        }));

            _videoTxPipeline.SetState(State.Playing);
            }

        }

        public void StartAudioTx(string destIP, int destPort, int codec)
        {
            string encoderName, payloaderName;
            switch (codec)
            {
                case 0:
                    encoderName = "mulawenc";
                    payloaderName = "rtppcmupay";
                    break;
                case 3:
                    encoderName = "gsmenc";
                    payloaderName = "rtpgsmpay";
                    break;
                case 8:
                    encoderName = "alawenc";
                    payloaderName = "rtppcmapay";
                    break;
                case 14:
                    encoderName = "ffenc_mp2";
                    payloaderName = "rtpmpapay";
                    break;
                default:
                    encoderName = "mulawenc";
                    payloaderName = "rtppcmupay";
                    break;
            }

            _audioTxPipeline = new Pipeline("audiotx-pipeline");
            Bin bin = (Bin)_audioTxPipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            _audioTxPipeline.Bus.AddSignalWatch();
            _audioTxPipeline.Bus.Message += (o, args) => BusCall(args.Message);

            //ds_src = ElementFactory.Make("dshowaudiosrc", "dshow-audio-in");
            Element dsSrc = ElementFactory.Make("audiotestsrc", "dshow-audio-in");
            Element audioConvert = ElementFactory.Make("audioconvert", "audio_convert");
            Element audioResample = ElementFactory.Make("audioresample", "audio_resample");
            Element encoder = ElementFactory.Make(encoderName, encoderName);

            Element payloader = ElementFactory.Make(payloaderName, payloaderName);
            Element udpSink = ElementFactory.Make("udpsink", "udp_sink");

            if ((dsSrc == null) || (audioConvert == null) || (audioResample == null) || (encoder == null) || (payloader == null) || (udpSink == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for Audio Tx pipeline!");
            }
            else
            {
                udpSink["host"] = destIP;
                udpSink["port"] = destPort;
                bin.Add(dsSrc, audioConvert, audioResample, encoder, payloader, udpSink);
                if (!Element.Link(dsSrc, audioConvert, audioResample, encoder, payloader, udpSink))
                {
                    Console.WriteLine("link failed between ds_src and udp_sink");
                }

                _audioTxPipeline.SetState(State.Playing);
            }
        }

        public void StartAudioRx(int recvPort, int codec)
        {
            Caps caps;
            string depayloaderName, decoderName;
            switch (codec)
            {
                case 0:
                    depayloaderName = "rtppcmudepay";
                    decoderName = "mulawdec";
                    caps = Caps.FromString("application/x-rtp,clock-rate=8000,payload=0");
                    break;
                case 3:
                    depayloaderName = "rtpgsmdepay";
                    decoderName = "gsmdec";
                    caps = Caps.FromString("application/x-rtp,clock-rate=8000,payload=3");
                    break;
                case 8:
                    depayloaderName = "rtppcmadepay";
                    decoderName = "alawdec";
                    caps = Caps.FromString("application/x-rtp,clock-rate=8000,payload=8");
                    break;
                case 14:
                    depayloaderName = "rtpmpadepay";
                    decoderName = "mad";
                    caps = Caps.FromString("application/x-rtp,media=(string)audio,clock-rate=(int)90000,encoding-name=(string)MPA,payload=(int)96");
                    break;
                default:
                    depayloaderName = "rtppcmudepay";
                    decoderName = "mulawdec";
                    caps = Caps.FromString("application/x-rtp,clock-rate=8000,payload=0");
                    break;
            }

            _audioRxPipeline = new Pipeline("audiorx-pipeline");
            Bin bin = (Bin)_audioRxPipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            _audioRxPipeline.Bus.AddSignalWatch();
            _audioRxPipeline.Bus.Message += (o, args) => BusCall(args.Message);


            Element udpSrc = ElementFactory.Make("udpsrc", "udp_src");
            Element depayloader = ElementFactory.Make(depayloaderName, depayloaderName);
            Element decoder = ElementFactory.Make(decoderName, decoderName);
            Element audioconvert = ElementFactory.Make("audioconvert", "audioconvert");
            Element audioresample = ElementFactory.Make("audioresample", "audio_resample");
            Element directsoundsink = ElementFactory.Make("directsoundsink", "directsoundsink");

            if ((udpSrc == null) || (depayloader == null) || (decoder == null) || (audioconvert == null) || (audioresample == null) || (directsoundsink == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for Audio Rx pipeline!");
            }
            else
            {
            udpSrc["port"] = recvPort;

            bin.Add(udpSrc, depayloader, decoder, audioconvert, audioresample, directsoundsink);

            if (!udpSrc.LinkFiltered(depayloader, caps))
            {
                Console.WriteLine("link failed between camera_src and tee");
            }


            if (!Element.Link(depayloader, decoder, audioconvert, audioresample, directsoundsink))
            {
                Console.WriteLine("link failed between udp_src and directsoundsink");
            }

            _audioRxPipeline.SetState(State.Playing);
            }

        }

        private bool BusCall(Message message)
        {
            if (GstLogEvent != null)
            {
                GstMessageEventArgs eventargs = new GstMessageEventArgs("type", "message");
                string msg;
                Enum err;
                eventargs.Type = message.Type.ToString();
                switch (message.Type)
                {
                    case MessageType.Error:
                        message.ParseError(out err, out msg);
                        eventargs.Message = message.Src.Name + ":\n Error - " + err + "\n" + msg;
                        break;
                    case MessageType.Eos:
                        eventargs.Message = message.Src.Name + ":\n" + "End of stream reached";
                        break;
                    case MessageType.Warning:
                        message.ParseWarning(out err, out msg);
                        eventargs.Message = message.Src.Name + ":\n Warning - " + err + "\n" + msg;
                        break;
                    default:
                        eventargs.Message = message.Src.Name + ":\n Default - " + "Entered bus call " + message.Type;
                        break;
                }
                GstLogEvent(this, eventargs);
            }
            return true;
        }

        public void StopVideoTx()
        {
            if (_videoTxPipeline != null)
            {
                _videoTxPipeline.SetState(State.Null);
                _videoTxPipeline.Dispose();
            }
        }

        public void StopVideoRx()
        {
            if (_videoRxPipeline != null)
            {
                _videoRxPipeline.SetState(State.Null);
                _videoRxPipeline.Dispose();
            }
        }

        public void StopAudioTx()
        {
            if (_audioTxPipeline != null)
            {
                _audioTxPipeline.SetState(State.Null);
                _audioTxPipeline.Dispose();
            }
        }

        public void StopAudioRx()
        {
            if (_audioRxPipeline != null)
            {
                _audioRxPipeline.SetState(State.Null);
                _audioRxPipeline.Dispose();
            }
        }


        internal void StopLoop()
        {
            if (_loop != null) _loop.Quit();
        }
    }
}
