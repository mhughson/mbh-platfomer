using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PicoX;
using System.Collections.Generic;
using System;
using TiledSharp;
using System.IO;
using System.Linq;
using Mono8;

namespace mbh_platformer
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    /// 

    // WORKING ON:
    // Issue where you press into side of rock while it is going up, pulls player up.
    // See: "C:\Users\Matt\Documents\Mono8\Mono8_636902690048336832.gif"

    /*
mget_ex which returns tile object (with bank offset, etc).
fget which takes tile object (and handles applying offset).

At game level, call the new functions everywhere.

Most calling function should probably go through all layers until fget returns non-zero
Alternatively we can have all level information on layer 0, but that will make hiding things behind tiles
impossible. << Do this for phase 1. Phase 2 add multi-layer sweep (at least for some checks like destructable rocks).
     */
    public class Game1 : PicoXGame
    {
        public static Game1 inst;

        public class PicoXObj : PicoXGame
        {
            #region UNUSED_API
            public override string GetMapString()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<int, string> GetMusicPaths()
            {
                throw new NotImplementedException();
            }

            public override string GetPalTextureString()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<string, object> GetScriptFunctions()
            {
                throw new NotImplementedException();
            }

            public override List<string> GetSheetPath()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<int, string> GetSoundEffectPaths()
            {
                throw new NotImplementedException();
            }
            #endregion

            public PicoXObj()
            {
                if (inst == null)
                {
                    throw new InvalidOperationException("Attempting to create PicoXObj before inst is set.");
                }
                P8API = inst;
            }

            public virtual void _preupdate() { }
            public virtual void _postupdate() { }
        }

        //helper for more complex
        //button press tracking.
        public class complex_button : PicoXObj
        {
            //state
            public bool is_pressed = false;//pressed this frame
            public bool is_down = false;//currently down
            public bool is_released = false;//released this frame
            public int ticks_down = 0;//how long down

            public int btn_id;

            public complex_button(int button_id)
            {
                btn_id = button_id;
            }

            public override void _update60()
            {
                base._update60();

                var self = this;

                //start with assumption
                //that not a new press.
                self.is_pressed = false;
                is_released = false;
                if (btn(btn_id))
                {
                    if (!self.is_down)
                    {

                        self.is_pressed = true;

                    }

                    self.is_down = true;

                    self.ticks_down += 1;
                }
                else
                {
                    is_released = is_down;

                    self.is_down = false;

                    self.is_pressed = false;

                    self.ticks_down = 0;
                }
            }
        }

        public class ui_widget : PicoXObj
        {
            protected List<ui_widget> children = new List<ui_widget>();
            public float x;
            public float y;
            protected bool has_focus;

            public virtual ui_widget add_child(ui_widget new_child)
            {
                children.Add(new_child);
                return this;
            }

            public virtual void clear_children()
            {
                children.Clear();
            }

            public virtual void draw_relative(float px, float py)
            {
                // No base implementation of draw beyond drawing children.
                foreach(var c in children)
                {
                    c.draw_relative(x + px, y + py);
                }
            }
            
            public virtual void on_focus_received()
            {
                has_focus = true;
            }

            public virtual void on_focus_lost()
            {
                has_focus = false;
            }

            public override void _draw()
            {
                draw_relative(0, 0);
            }

            public override void _update60()
            {
                // c-style loop so that clearing the list while iterating doesn't crash.
                for(int i = 0; i < children.Count; i++)
                {
                    children[i]._update60();
                }
            }
        }

        // Hard coded implementation of main menu. Need to generalize to take callback
        // functions per child, etc.
        public class ui_menu_scene : ui_widget
        {
            int cur_index = 1;

            public override void _update60()
            {
                if (children.Count > 0)
                {
                    int new_index = cur_index;
                    // up
                    if (btnp(2))
                    {
                        new_index = (int)max(0, cur_index - 1);
                    }
                    // down
                    if (btnp(3))
                    {
                        new_index = (int)min(children.Count - 1, cur_index + 1);
                    }

                    if (inst.btnp_confirm())
                    {
                        if (cur_index == 0)
                        {
                            // Clear the save game...
                            inst.clear_save();
                            inst.pc.reload();
                        }
                        inst.queued_map = "Content/raw/map_ow_top.tmx";
                        inst.set_game_state(game_state.level_trans_exit);
                    }

                    //if (cur_index != new_index)
                    {
                        children[cur_index].on_focus_lost();
                        children[new_index].on_focus_received();
                        cur_index = new_index;
                    }
                }
            }
        }

        public class ui_menu_scene_list_item : ui_widget
        {
            public Action on_action_delegate;

            public override void draw_relative(float px, float py)
            {
                base.draw_relative(px, py);

                if (has_focus)
                {
                    float fx = x + px;
                    float fy = y + py;
                    rectfill(fx - 8, fy, fx - 4, fy + 4, 5);
                }
            }
        }

        public class ui_menu_scene_list : ui_widget
        {
            public Action on_close_delegate;

            int cur_index = 0;

            public override ui_widget add_child(ui_widget new_child)
            {
                System.Diagnostics.Debug.Assert(new_child as ui_menu_scene_list_item != null);

                return base.add_child(new_child);
            }

            public override void _update60()
            {
                if (children.Count > 0)
                {
                    int new_index = cur_index;
                    // up
                    if (btnp(2))
                    {
                        new_index = (int)max(0, cur_index - 1);
                    }
                    // down
                    if (btnp(3))
                    {
                        new_index = (int)min(children.Count - 1, cur_index + 1);
                    }

                    if (btnp(4))
                    {
                        on_close_delegate?.Invoke();
                    }
                    else if (inst.btnp_confirm())
                    {
                        (children[cur_index] as ui_menu_scene_list_item).on_action_delegate?.Invoke();
                    }

                    //if (cur_index != new_index)
                    {
                        children[cur_index].on_focus_lost();
                        children[new_index].on_focus_received();
                        cur_index = new_index;
                    }
                }
            }
        }

        public class ui_text : ui_widget
        {
            public string display_string;
            public bool outline;
            public int color;
            public int color_outline;

            public override void draw_relative(float px, float py)
            {
                base.draw_relative(px, py);

                float fx = x + px;
                float fy = y + py;

                if (has_focus)
                {
                    rectfill(fx - 8, fy, fx - 4, fy + 4, color);
                }

                if (outline)
                {
                    inst.printo(display_string, fx, fy, color, color_outline);
                }
                else
                {
                    print(display_string, fx, fy, color);
                }
            }
        }

        public class ui_box : ui_widget
        {
            public float width;
            public float height;
            public int color;
            public bool fill;

            public override void draw_relative(float px, float py)
            {
                base.draw_relative(px, py);

                if (fill)
                {
                    rectfill(x + px, y + py, x + px + width, y + py + height, color);
                }
                else
                {
                    rect(x + px, y + py, x + px + width, y + py + height, color);
                }
            }
        }

        public class sprite : PicoXObj
        {
            public class flying_def
            {
                public bool horz = true;
                public int duration = 0;
                public int dist = 0;
            }
            public float x;
            public float y;
            public float x_initial;
            public float y_initial;
            public float dx;
            public float dy;
            public int w;
            public int h;
            public int cw;
            public int ch;
            public int cx_offset { get; protected set; } = 0;
            public int cy_offset { get; protected set; } = 0;
            public float cx { get { return x + cx_offset; } }
            public float cy { get { return y + cy_offset; } }
            public int jump_hold_time = 0;//how long jump is held
            public byte grounded = 0;//on ground
            public int airtime = 0;//time since groundeds
            public float scaley = 0;

            public bool is_platform = false;
            public bool stay_on = false;
            public bool launched = false;

            public flying_def flying = null;
            public virtual int get_hp_max() { return 1; }
            public float hp = 1;
            public float attack_power = 1;

            protected int invul_time = 0;
            protected int invul_time_on_hit = 120;

            public int bank = 0;

            public class anim
            {
                public int ticks;
                public int[][] frames;
                public bool? loop;
                public int? w;
                public int? h;
            }

            static public int[] create_anim_frame(int start_sprite, int w, int h, int zeroed_rows_at_top = 0)
             {
                int[] frame = new int[w * h];
                int count = 0;
                for (int j = 0; j < h; j++)
                {
                    for (int i = 0; i < w; i++)
                    {
                        int sprite_id = (start_sprite + i) + (16 * j);
                        if (j < zeroed_rows_at_top)
                        {
                            sprite_id = 0;
                        }
                        frame[count] = sprite_id;
                        count++;
                    }
                }

                return frame;
            }

            //animation definitions.
            //use with set_anim()
            public Dictionary<string, anim> anims;

            public string curanim = "";//currently playing animation
            public int curframe = 0;//curent frame of animation.
            public int animtick = 0;//ticks until next frame should show.
            public bool flipx = false;//show sprite be flipped.
            public bool flipy = false;

            public delegate void on_anim_done_delegate(string anim_name);
            public on_anim_done_delegate event_on_anim_done;

            //request new animation to play.
            public void set_anim(string anim)
            {
                var self = this;
                if (anim == self.curanim) return;//early out.
                var a = self.anims[anim];

                self.animtick = a.ticks;//ticks count down.
                self.curanim = anim;
                self.curframe = 0;
            }

            public void tick_anim()
            {
                if (anims == null || !anims.ContainsKey(curanim))
                {
                    return;
                }

                animtick -= 1;
                if (animtick <= 0)
                {
                    curframe += 1;

                    var a = anims[curanim];
                    animtick = a.ticks; //reset timer
                    if (curframe >= a.frames.Length)
                    {
                        if (a.loop == false)
                        {
                            // back up the frame counter so that we sit on the last frame.
                            // do it before calling anim_done, because that might actually trigger
                            // a new animation and we don't want to mess with its frame.
                            curframe--;
                            event_on_anim_done?.Invoke(curanim); // TODO_PORT
                        }
                        else
                        {
                            // TODO: Was it intentional that this is only called when looping?
                            event_on_anim_done?.Invoke(curanim); // TODO_PORT
                            curframe = 0; //loop
                        }
                    }
                }
            }

            public override void _update60()
            {
                invul_time = (int)max(0, invul_time - 1);
                base._update60();

                tick_anim();
            }
            
            public virtual void push_pal()
            {
                inst.apply_pal(inst.get_cur_pal(true));
            }

            public virtual void pop_pal()
            {
                pal();
            }

            public override void _draw()
            {
                var self = this;
                base._draw();

                inst.bset(bank);

                if (anims != null && !String.IsNullOrEmpty(curanim))
                {

                    var a = anims[curanim];
                    int[] frame = a.frames[curframe];

                    // TODO: Mono8 Port
                    //if (pal) push_pal(pal)

                    // Mono8 Port: Starting with table only style.
                    //if type(frame) == "table" then
                    if (invul_time == 0 || invul_time % 2 == 0)
                    {
                        var final_w = a.w ?? w;
                        var final_h = a.h ?? h;
                        var final_w_half = flr(final_w * 0.5f);
                        var final_h_half = flr(final_h * 0.5f);

                        var start_x = x - (final_w_half);
                        var start_y = y - (final_h_half);

                        var count = 0;

                        var num_vert = flr(final_h / 8);
                        var num_horz = flr(final_w / 8);

                        var inc_x = 8;
                        var inc_y = 8;

                        if (flipx)
                        {
                            start_x = start_x + ((num_horz - 1) * 8);
                            inc_x = -8;
                        }


                        if (flipy)
                        {
                            start_y = start_y + ((num_vert - 1) * 8);
                            inc_y = -8;
                        }

                        var y2 = start_y;

                        for (int v_count = 0; v_count < num_vert; v_count++)
                        {
                            var x2 = start_x;

                            for (int h_count = 0; h_count < num_horz; h_count++)
                            {
                                //draw in frame order, but from
                                // right to left.
                                var f = frame[count];

                                // Don't draw sprite 0. This allows us to use that as a special 
                                // sprite in our animation data.
                                if (f != 0)
                                {

                                    var flipx2 = flipx;

                                    var flipy2 = flipy;

                                    // Mono8 Port: frame is an int can can't be null.
                                    //if (f != null)
                                    {
                                        // TODO: This doesn't properly support flipping collections of tiles (eg. turn a 3 tile high 
                                        // sprite upside down. it will flip each tile independently).
                                        if (f < 0)
                                        {
                                            f = (int)abs(f);

                                            flipx2 = !flipx2;
                                        }

                                        // Hack to allow flipping Y. Add 512 to your sprite id.
                                        if (f >= 9999)
                                        {
                                            f -= 9999;

                                            flipy2 = !flipy2;
                                        }

                                        sspr((f * 8) % 128, flr((f / 16)) * 8, 8, 8,
                                            x2, y2 - (scaley * v_count) + (((8) - (8 - scaley)) * num_vert), 8, 8 - scaley,
                                            flipx2, flipy2);

                                    }
                                }
                                count += 1;

                                x2 += inc_x;

                            }
                            y2 += inc_y;

                        }
                    }
                }

                if (inst.debug_draw_enabled)
                {
                    //if (inst.time_in_state % 2 == 0)
                    {
                        rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 14);
                        rect(cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2, 15);
                    }
                    pset(x, y, 8);
                    pset(cx, cy, 9);

                    // bottom
                    //var offset_x = self.cw / 3.0f;
                    //var offset_y = self.ch / 2.0f;
                    //for (float i = -(offset_x); i <= (offset_x); i += 2)
                    //{
                    //    pset(x + i, y + offset_y, 9);
                    //}

                    // sides
                }
            }

            public virtual void on_collide_side(sprite target) { }
        }

        public class simple_fx : sprite
        {
            public simple_fx()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = false,
                            ticks=4,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(112, 4, 3),
                                create_anim_frame(116, 4, 3),
                                create_anim_frame(120, 4, 3),
                            }
                        }
                    },
                };

                set_anim("explode");

                x = 64;
                y = 64;
                w = 32;
                h = 24;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs_remove_queue.Add(this);
                };
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                base._update60();
            }
        }

        public class simple_fx_dust : simple_fx
        {
            public simple_fx_dust()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = false,
                            ticks=4,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(456, 2, 2),
                                create_anim_frame(458, 2, 2),
                            }
                        }
                    },
                };

                set_anim("explode");
                
                w = 16;
                h = 16;
            }
        }

        public class simple_fx_particle : simple_fx
        {
            public simple_fx_particle(float dir_x, float dir_y, int sprite_id)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = true,
                            ticks=1,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(sprite_id, 1, 1),
                            }
                        }
                    },
                };

                set_anim("default");

                x = 0;
                y = 0;
                w = 8;
                h = 8;

                dx = dir_x;
                dy = dir_y;


                event_on_anim_done = null;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                x += dx;
                dy += 0.3f; // grav
                y += dy;

                if (inst.game_cam.is_obj_off_screen(this))
                {
                    inst.objs_remove_queue.Add(this);
                }
            }
        }

        public class simple_fx_death_spark : simple_fx
        {
            float dir_x;
            float dir_y;

            public simple_fx_death_spark(float dir_x, float dir_y) : base()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = true,
                            ticks=4,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(224, 2, 2),
                                create_anim_frame(226, 2, 2),
                                create_anim_frame(228, 2, 2),
                                create_anim_frame(226, 2, 2),
                            }
                        }
                    },
                };

                set_anim("explode");

                x = 64;
                y = 64;
                w = 16;
                h = 16;

                this.dir_x = dir_x;
                this.dir_y = dir_y;

                event_on_anim_done = null;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                x += dir_x;
                y += dir_y;
            }
        }

        public class simple_fx_rotor : simple_fx
        {
            public simple_fx_rotor()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = false,
                            ticks=5,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                // dupes to give it time to leave screen.
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                            }
                        }
                    },
                };

                set_anim("explode");

                w = 16;
                h = 8;
            }

            public override void _update60()
            {
                base._update60();

                dy += -0.1f; // grav
                y += dy;
            }
        }

        public class simple_fx_projectile_death : simple_fx
        {
            public simple_fx_projectile_death() : base()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = false,
                            ticks = 5,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(456, 2, 2),
                                create_anim_frame(458, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;

                dx = 0;
                dy = 0;
            }
        }

        public class simple_fx_tile_glimmer : simple_fx
        {
            public simple_fx_tile_glimmer() : base()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = false,
                            ticks = 2,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(512, 2, 2),
                                create_anim_frame(514, 2, 2),
                                create_anim_frame(516, 2, 2),
                                create_anim_frame(518, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;

                dx = 0;
                dy = 0;

                bank = 5;
            }
        }

        public class badguy : sprite
        {
            int local_ticks = 0;

            float max_dx = 9999;
            float max_dy = 9999;

            protected float grav = 0.1f;

            protected bool solid = true;

            bool bounced = false;

            public int dead_time = -1;

            public bool touch_damage = false;

            bool cleared_attacker = false;

            protected bool has_rock_armor = false;

            // Data used to spawn this object, and eventually used to respawn this object.
            // If not set, it will not respawn on death.
            public TmxObject respawn_data;

            // How many ticks to wait before respawn after death.
            public int respawn_delay;

            public badguy(float dir)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(320, 2, 2),
                                create_anim_frame(322, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                dx = 0.5f * dir;

                flipx = dir < 0;

                stay_on = true;

                hp = 1;
            }

            public void delete_object(bool supress_respawn = false)
            {
                inst.objs_remove_queue.Add(this);

                if (respawn_data != null && !supress_respawn)
                {
                    //TmxObject temp = respawn_data;
                    Action callback = new Action(() =>
                    {
                        // respawn.
                        inst.ParseTmxObjectToBadGuy(respawn_data);
                    });

                    timer_callback t = new timer_callback(respawn_delay, callback);
                    inst.objs_add_queue.Add(t);
                }
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                local_ticks += 1;

                //limit walk speed

                dx = mid(-max_dx, dx, max_dx);

                //move in x

                if (!launched)
                {
                    x += dx;
                }


                if (solid)
                {
                    var old_dx = dx;
                    Vector2 hit_point;
                    if (inst.collide_side(this, out hit_point))
                    {
                        dx = -old_dx;
                    }
                }


                //move in y

                dy += grav;

                dy = mid(-max_dy, dy, max_dy);

                y += dy;

                // TODO:
                if (flying != null && dead_time == -1)
                {
                    var t60 = local_ticks / 60.0f;

                    if (flying.horz == true)
                    {
                        float start_x = x;
                        // cos goes -1 -> 1 -> -1, so flip that to 1 -> -1 ..., then +1 to
                        // put it 0 -> 2, then half that for 0 -> 1 -> 0
                        x = x_initial + ((-cos(t60 / flying.duration) + 1) * 0.5f) * flying.dist;
                        if (x < start_x)
                        {
                            flipx = true;
                        }
                        else if (x > start_x)
                        {
                            flipx = false;
                        }
                    }
                    else
                    {
                        y = y_initial + ((sin(t60 / flying.duration) + 1) * 0.5f) * flying.dist;

                    }
                }

                grounded = 0;
                if (solid)
                {
                    Vector2 hit_point;
                    if (inst.collide_floor(this, out hit_point))
                    {
                        launched = false;
                    }
                    inst.collide_roof(this);
                }

                // ENEMY SPECIFIC:
                // 

                if (dx < 0)
                {
                    flipx = true;
                }
                else if (dx > 0) // let 0 maintain.
                {
                    flipx = false;
                }

                // TODO:
                if (dead_time >= 0)
                {
                    dead_time -= 1;

                    if (dead_time <= 0/* || !inst.game_cam.is_obj_in_play_area(this)*/)
                    {
                        // Only play this little explosion anim if we are timing out on the bounced sequence.
                        // Not for cases like stomping.
                        if (bounced)
                        {
                            inst.objs_add_queue.Add(new simple_fx() { x = x, y = y });
                        }
                        delete_object();
                    }
                    else
                    {
                        if (!cleared_attacker)
                        {
                            if (!inst.intersects_obj_obj(inst.pc.pawn, this))
                            {
                                cleared_attacker = true;
                            }
                        }

                        if (cleared_attacker && (bounced || launched) && !touch_damage && inst.intersects_obj_obj(inst.pc.pawn, this))
                        {
                            if (inst.pc.pawn.get_is_dashing() || inst.pc.pawn.dashing_last_frame)
                            {
                                on_bounce(inst.pc.pawn, true);
                                Vector2 pos = new Vector2(x, y);
                                inst.pc.pawn.start_dash_bounce(ref pos);
                            }
                        }
                    }

                    return;
                }

                if (launched)
                {
                    if (!cleared_attacker)
                    {
                        if (!inst.intersects_obj_obj(inst.pc.pawn, this))
                        {
                            cleared_attacker = true;
                        }
                    }

                    if (cleared_attacker && launched && !touch_damage && inst.intersects_obj_obj(inst.pc.pawn, this))
                    {
                        if (inst.pc.pawn.get_is_dashing() || inst.pc.pawn.dashing_last_frame)
                        {
                            on_bounce(inst.pc.pawn, true);
                            Vector2 pos = new Vector2(x, y);
                            inst.pc.pawn.start_dash_bounce(ref pos);
                            launched = false;
                        }
                    }
                }

                // TODO
                //if (dead_time == -1 && !inst.p.pawn.is_dead && inst.p.pawn.pipe == null)
                if (dead_time == -1 && inst.cur_game_state != game_state.gameplay_dead)
                {

                    if (inst.intersects_obj_obj(inst.pc.pawn, this))
                    {
                        // TODO
                        //if (inst.p.pawn.star_time > 0)
                        //{
                        //    self: on_bounce(p1);
                        //}
                        //else

                        //if (touch_damage)
                        //{
                        //    inst.p.pawn.on_take_hit(this);
                        //}
                        //else
                        {
                            //feet pos.
                            var player_bottom = inst.pc.pawn.cy + (inst.pc.pawn.ch * 0.5f);

                            if ((inst.pc.pawn.get_is_dashing() || inst.pc.pawn.dashing_last_frame) && !touch_damage)
                            {
                                bool rock_smashable = inst.pc.has_artifact(artifacts.rock_smasher) && has_rock_armor;
                                bool had_rock_armor = has_rock_armor;

                                //Vector2 pos = new Vector2(x, y);
                                //inst.p.pawn.start_dash_bounce(ref pos);
                                //dx = inst.p.pawn.dx;
                                //inst.p.pawn.dx *= -1;
                                if (!has_rock_armor)
                                {
                                    on_bounce(inst.pc.pawn);
                                }
                                else if (rock_smashable)
                                {
                                    int mx = flr(x / 8);
                                    int my = flr(y / 8);
                                    var grid_pos = inst.map_pos_to_meta_tile(mx, my);

                                    for (int i = 0; i <= 1; i++)
                                    {
                                        for (int j = 0; j <= 1; j++)
                                        {
                                            var final_x = (grid_pos.X + i) * 8 + 4;

                                            var final_y = (grid_pos.Y + j) * 8 + 4;
                                            inst.objs_add_queue.Add(
                                                new simple_fx_particle(-1 + (i * 2), (1 - j + 1) * -3, (8 + i) + ((52 + j) * 16))
                                                {
                                                    x = final_x,
                                                    y = final_y,
                                                });
                                        }
                                    }

                                    has_rock_armor = false;
                                }

                                if (flying != null || had_rock_armor)
                                {
                                    Vector2 pos = new Vector2(x, y);
                                    inst.pc.pawn.start_dash_bounce(ref pos);

                                    // Move the the edge of the bad guy to avoid hitting the next frame.
                                    float delta_x = inst.pc.pawn.x - x;
                                    inst.pc.pawn.x = x + Math.Sign(delta_x) * (cw * 0.5f + inst.pc.pawn.cw * 0.5f);

                                }
                            }
                            else if (cy > player_bottom)
                            {
                                if (inst.pc.pawn.dy >= 0 && !touch_damage)
                                {
                                    on_stomp();
                                }
                            }
                            else
                            {
                                //self:on_attack(p1);
                                inst.pc.pawn.on_take_hit(this);
                            }
                        }
                    }
                }

                base._update60();
            }

            public override void _draw()
            {
                base._draw();

                if (has_rock_armor)
                {
                    spr(840, x - 8, y - 8, 2, 2);
                }
            }

            public virtual void on_bounce(sprite attacker, bool ignore_dead_time = false)
            {
                hp = max(0, hp - 1);

                if ((dead_time == -1 || ignore_dead_time) && hp == 0)
                {
                    dead_time = 120; // enough time to clear a screen and a bit.

                    dx = Math.Sign(attacker.dx) * 0.5f;

                    dy = -3.5f;

                    solid = false;

                    flipy = true;

                    bounced = true;

                    // TODO:
                    //flying = null;
                    grav = 0.1f;

                    cleared_attacker = false;

                }
            }

            public void on_launch()
            {
                dy = -3;
                cleared_attacker = false;
                launched = true;
            }

            protected virtual void on_stomp()
            {
                scaley = 4;
                dead_time = 60;
                dx = 0;
                dy = 0;
                float amount = -0.5f;
                if (btn(4))
                {
                    amount = -1.0f;
                }
                inst.pc.pawn.dy = inst.pc.pawn.max_dy * amount;
            }
        }

        public class chopper_body : badguy
        {
            // used to track if the body is in free fall, and should be deleted (for respawn
            // to not get stuck waiting).
            // Alternatively, we could respawn the chopper when the body gets spawned,
            // but we will still want to prevent too many from spawning for perf reasons.
            int ticks = 0;
            bool landed = false;

            public chopper_body() : base(0)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(340, 2, 3),
                                create_anim_frame(342, 2, 3),
                            }
                        }
                    },
                };

                set_anim("default");
            }

            public override void _update60()
            {
                base._update60();

                // Handle falling offscreen without ever hitting ground.
                landed |= grounded != 0;

                ticks++;
                if (ticks > 60 && !landed)
                {
                    inst.objs_add_queue.Add(new simple_fx() { x = x, y = y });
                    delete_object();
                }
            }
        }

        public class chopper : badguy
        {
            // How long the object has been alive, used for fade in on respawn.
            int lifetime = 0;

            public chopper(int duration, int dist) : base(0)
            {
                flying = new flying_def()
                {
                    duration = duration,
                    dist = dist,
                    horz = true,
                };

                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(324, 2, 3),
                                create_anim_frame(326, 2, 3),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 24;
                cw = 16;
                ch = 16;
                cy_offset = 4;

                dx = 0;
                dy = 0;
                grav = 0;

                stay_on = false;
                flipx = true;
                solid = false;
            }

            protected override void on_stomp()
            {
                base.on_stomp();

                inst.objs_add_queue.Add(new chopper_body()
                {
                    x = x,
                    y = y + 4,
                    flipx = flipx,
                    dx = 0,
                    dy = 0,
                    respawn_data = respawn_data,
                    respawn_delay = respawn_delay,
                });
                inst.objs_add_queue.Add(new simple_fx_rotor()
                {
                    x = x,
                    y = y - 8,
                    flipx = flipx,
                    dx = 0,
                    dy = -0.5f,
                });

                // Intentionally don't respawn here. Chopper body will take over.
                delete_object(true);
            }

            public override void _update60()
            {
                lifetime++;
                base._update60();
            }

            public override void _draw()
            {
                int time_step = 10;
                if (lifetime < time_step)
                {
                    inst.apply_pal(inst.fade_table[2]);
                }
                else if (lifetime < time_step * 2)
                {
                    inst.apply_pal(inst.fade_table[1]);
                }
                else if (lifetime < time_step * 3)
                {
                    inst.apply_pal(inst.fade_table[0]);
                }
                base._draw();
                pal();
            }
        }

        public class lava_splash : badguy
        {
            public lava_splash(float dir) : base(dir)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = true,
                            ticks=10,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(768, 3, 2),
                                create_anim_frame(771, 3, 2),
                                create_anim_frame(774, 3, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 24;
                h = 16;
                cw = 24;
                ch = 8;
                cy_offset = 4;

                dx = 0;

                touch_damage = true;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs_remove_queue.Add(this);
                };
            }

            public override void _update60()
            {
                base._update60();

                //if (curframe == 1)
                //{
                //    touch_damage = true;
                //}
                //else
                //{
                //    touch_damage = false;
                //}
            }
        }

        public class steam_spawner : sprite
        {
            int ticks = 0;

            public steam_spawner()
            {

            }

            public override void _update60()
            {
                base._update60();

                ticks++;

                if (ticks % 60 == 0)
                {
                    inst.objs_add_queue.Add(new steam_splash() { x = x, y = y });
                }
            }
        }

        public class steam_splash : badguy
        {
            public steam_splash() : base(0)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = true,
                            ticks=10,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(768, 2, 2),
                                create_anim_frame(771, 2, 2),
                                create_anim_frame(774, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 8;
                ch = 8;
                cy_offset = 4;

                dx = 0;

                touch_damage = true;
                attack_power = 0.25f;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs_remove_queue.Add(this);
                };
            }

            public override void _update60()
            {
                base._update60();
            }
        }

        public class water_splash : simple_fx
        {
            public water_splash()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = true,
                            ticks=10,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(768, 2, 2),
                                create_anim_frame(771, 2, 2),
                                create_anim_frame(774, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
            }
        }

        public class lava_blast_spawner : sprite
        {
            int ticks;
            float dir;
            float speed;

            public lava_blast_spawner(float dir)
            {
                this.dir = dir;
                speed = 16;
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                ticks++;

                if (ticks % 5 == 0)
                {
                    x += dir * speed;// * (rnd(8) + 8);
                    if (fget(mget_tiledata(flr(x / 8.0f), flr(y / 8.0f)), 0) || !fget(mget_tiledata(flr(x / 8.0f), flr(y / 8.0f) + 1), 0))
                    {
                        inst.objs_remove_queue.Add(this);
                        return;
                    }
                    inst.objs_add_queue.Add(new lava_splash(dir) { x = x, y = y, /*, dx = rnd(1) * dir*/});
                }


                base._update60();
            }
        }

        public class lava_blaster : badguy
        {
            int idle_ticks = 0;

            public lava_blaster(float dir) : base(dir)
            {

                anims = new Dictionary<string, anim>()
                {
                    {
                        "idle",
                        new anim()
                        {
                            loop = true,
                            ticks=30,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(400, 3, 3),
                                create_anim_frame(403, 3, 3),
                            }
                        }
                    },
                    {
                        "open_mouth",
                        new anim()
                        {
                            loop = false,
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(406, 3, 3),
                                create_anim_frame(409, 3, 3),
                            }
                        }
                    },
                    {
                        "fire",
                        new anim()
                        {
                            loop = false,
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(406, 3, 3),
                                create_anim_frame(400, 3, 3),
                            }
                        }
                    },
                };

                set_anim("idle");

                w = 24;
                h = 24;
                cw = 24;
                ch = 24;

                dx = 0;

                has_rock_armor = true;

                event_on_anim_done += delegate (string anim_name)
                {
                    if (dead_time > 0)
                    {
                        return;
                    }
                    if (anim_name == "open_mouth")
                    {
                        inst.objs_add_queue.Add(new lava_blast_spawner(flipx ? -1 : 1) { x = x, y = y + 4 });
                        set_anim("fire");
                    }
                    else if (anim_name == "fire")
                    {
                        set_anim("idle");
                    }
                };
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                if (dead_time > 0)
                {
                    set_anim("open_mouth");
                }
                else if (curanim == "idle")
                {
                    idle_ticks++;
                    if (idle_ticks > 180)
                    {
                        set_anim("open_mouth");
                        idle_ticks = 0;
                    }
                }
                base._update60();
            }
        }



        public class dart_gun : sprite
        {
            int ticks = 0;

            // How long to wait before starting to shoot.
            public int start_delay = 0;

            // The number of ticks to wait between each shot, after start_delay has expired.
            public int firing_delay = 240;

            // Has the start_delay expired.
            bool started = false;

            float dir_x;
            float dir_y;
            int bullet_life_time = 480; // enough to leave the screen

            public dart_gun(float dir_x, float dir_y) : base()
            {

                int frame = 352;
                if (dir_y != 0)
                {
                    frame = 354;
                }

                anims = new Dictionary<string, anim>()
                {
                    {
                        "idle",
                        new anim()
                        {
                            loop = false,
                            ticks= 1,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(frame, 2, 2),
                            }
                        }
                    },
                };

                set_anim("idle");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                dx = 0;
                dy = 0;

                // Can't have no direction and can't be diagonal.
                System.Diagnostics.Debug.Assert(dir_x != dir_y);

                this.dir_x = dir_x;
                this.dir_y = dir_y;

                if (dir_x < 0)
                {
                    flipx = true;
                }
                if (dir_y < 0)
                {
                    flipy = true;
                }

                bullet_life_time = 480;
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                if (!started)
                {
                    if (ticks == start_delay)
                    {
                        ticks = 0;
                        started = true;
                    }
                }
                
                // Not an else so that fire_shot is called the frame that start_delay expires.
                if (started)
                {
                    if (ticks >= firing_delay)
                    {
                        ticks = 0;
                    }

                    // Handle both the frame that started gets set to true, and when firing delay loops.
                    if (ticks == 0)
                    {
                        fire_shot();
                    }
                }

                base._update60();
                
                ticks++;
            }

            public override void push_pal()
            {
                base.push_pal();

                //if (ticks % 4 >= 2)
                {
                    if (firing_delay - ticks < 10)
                    {
                        inst.apply_pal(inst.bright_table[2]);
                    }
                }
            }

            void fire_shot()
            {
                projectile p = new projectile(dir_x, dir_y, bullet_life_time)
                {
                    x = x + dir_x * 8.0f,
                    y = y + dir_y * 8.0f,
                };
                inst.objs_add_queue.Add(p);
            }
        }

        public class projectile : badguy
        {
            int life_span;

            public projectile(float dir_x, float dir_y, int life_span) : base(0)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = false,
                            ticks = 1,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(360, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 8;
                ch = 8;

                grav = 0.0f;

                touch_damage = true;
                attack_power = 1.0f;

                solid = false;

                float speed = 1.0f;
                dx = dir_x * speed;
                dy = dir_y * speed;

                this.life_span = life_span;

                // Currently assuming that a 0,0 direction is an error, but might want to remove this if we ever
                // want just a floating projectile.
                System.Diagnostics.Debug.Assert(dir_x != 0 || dir_y != 0);
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                if (life_span <= 0)
                {
                    inst.objs_remove_queue.Add(this);
                    return;
                }

                for(int i = 0; i < inst.objs.Count; i++)
                {
                    platform o = inst.objs[i] as platform;
                    if (o != null)
                    {
                        if( inst.intersects_obj_obj(this, o))
                        {
                            inst.objs_remove_queue.Add(this);
                            inst.objs_add_queue.Add(new simple_fx_projectile_death() { x = x, y = y });
                            return;
                        }
                    }
                    
                }

                // Hit a wall?
                if (fget(mget_tiledata(flr(x / 8.0f), flr(y / 8.0f)), 0))
                {
                    inst.objs_remove_queue.Add(this);
                    inst.objs_add_queue.Add(new simple_fx_projectile_death() { x = x, y = y });
                    return;
                }

                base._update60();

                life_span--;
            }
        }

        public class rock : sprite
        {
            public rock()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(832, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                is_platform = true;
            }

            protected float tick = 0;
            protected bool hit_this_frame = false;
            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                var self = this;

                hit_this_frame = false;

                // TODO: Error - This assumes a collision means you are standing on top!
                var touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p.pawn);

                var old_x = self.x;

                var old_y = self.y;

                //base._update60();
                tick += 0.0025f;
                //y = 964 - (cos(tick) * 64.0f);
                //y = 964;// - (cos(tick) * 64.0f);
                x += (cos(tick)) * 1.0f; //80 - (cos(tick) * 64.0f);
                //y += (sin(tick)) * 1.0f; //80 - (cos(tick) * 64.0f);

                if (touching_player)
                {
                    hit_this_frame = true;
                    inst.pc.pawn.x += self.x - old_x;
                    inst.pc.pawn.y += self.y - old_y;

                    //inst.p.pawn.dx = self.x - old_x;
                    //inst.p.pawn.dy = self.y - old_y;

                    //inst.p.pawn.platformed = true;
                }
                else
                {
                    inst.pc.pawn.platformed = false;
                }

                //if (tick >= 0.5f)
                //{
                //    inst.change_meta_tile(flr(x / 8), flr(y / 8), new int[] { 868, 869, 884, 885 });
                //    inst.objs.Remove(this);
                //}

                // Should be handled by collide side.
                //if (inst.p.pawn.dash_time > 0 && inst.intersects_obj_obj(this, inst.p.pawn))
                //{
                //    Vector2 v = new Vector2(x - (x - inst.p.pawn.x) * 0.5f, y - (y - inst.p.pawn.y) * 0.5f);
                //    inst.p.pawn.start_dash_bounce(ref v);
                //}
            }
        }

        public class rock_pendulum : rock
        {
            float dir_x;

            public rock_pendulum(float dir_x) : base()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(6, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                bank = 1;

                this.dir_x = dir_x;
            }
            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                var self = this;

                hit_this_frame = false;

                // TODO: Error - This assumes a collision means you are standing on top!
                var touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p.pawn);

                var old_x = self.x;

                var old_y = self.y;

                //base._update60();
                tick += 0.01f;
                //y = 964 - (cos(tick) * 64.0f);
                //y = 964;// - (cos(tick) * 64.0f);
                x += (cos(tick)) * 4.0f * dir_x; //80 - (cos(tick) * 64.0f);
                y += (sin(tick)) * 4.0f; //80 - (cos(tick) * 64.0f);

                if (touching_player)
                {
                    hit_this_frame = true;
                    inst.pc.pawn.x += self.x - old_x;
                    inst.pc.pawn.y += self.y - old_y;

                    //inst.p.pawn.dx = self.x - old_x;
                    //inst.p.pawn.dy = self.y - old_y;

                    //inst.p.pawn.platformed = true;
                }
                else
                {
                    inst.pc.pawn.platformed = false;
                }

                if (tick >= 0.5f)
                {
                    inst.change_meta_tile(flr(x / 8), flr(y / 8), new int[] { 36, 37, 52, 53 }, 1);
                    inst.objs_remove_queue.Add(this);
                }

                // Should be handled by collide side.
                //if (inst.p.pawn.dash_time > 0 && inst.intersects_obj_obj(this, inst.p.pawn))
                //{
                //    Vector2 v = new Vector2(x - (x - inst.p.pawn.x) * 0.5f, y - (y - inst.p.pawn.y) * 0.5f);
                //    inst.p.pawn.start_dash_bounce(ref v);
                //}
            }
        }

        public class physical_object : sprite
        {
            protected float gravity = 0; // none
            protected float ground_friction = 1.0f; // none
            protected float bounce_friction = 1.0f; // none

            public physical_object() : base()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(832, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                is_platform = true;
            }

            public override void _update60()
            {
                base._update60();

                // Store the original position so that we can calculate a delta at the end of the update,
                // and move objects on top of this one at the same rate.
                var old_x = x;
                var old_y = y;

                x += dx;

                dy += gravity;
                y += dy;

                grounded = 0;
                Vector2 hit_point;
                if (inst.collide_floor(this, out hit_point))
                {
                    dx *= ground_friction;
                }

                var old_dx = dx;
                if (inst.collide_side(this, out hit_point))
                {
                    dx = -old_dx * bounce_friction;
                }
                inst.collide_roof(this);

                // After moving this object, not try to move the player.
                //

                // TODO: Error - This assumes a collision means you are standing on top!
                bool touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, cx, cy, cw * 0.5f, (ch + 2) * 0.5f);

                if (touching_player)
                {
                    inst.pc.pawn.x += x - old_x;
                    inst.pc.pawn.y += y - old_y;
                }
            }
        }

        public class push_block : physical_object
        {
            public push_block() : base()
            {
                gravity = 0.18f; // 0.3f;
                ground_friction = 0.98f;
                bounce_friction = 0.5f;
            }

            public override void _update60()
            {
                base._update60();

                float min_speed = 0.05f;
                if (abs(dx) <= min_speed)
                {
                    dx = 0;
                }

                if (abs(dx) > 0)
                {
                    foreach (var o in inst.objs)
                    {
                        badguy bg = o as badguy;
                        if (bg != null)
                        {
                            if (inst.intersects_obj_obj(this, bg))
                            {
                                bg.on_bounce(this);
                            }
                        }
                    }
                }

                if (grounded != 0 && abs(dx) > 0)
                {
                    inst.objs_add_queue.Add(new simple_fx_dust() { x = cx + rnd(8) - 4, y = cy + ch * 0.5f });
                }
            }

            public override void on_collide_side(sprite target)
            {
                base.on_collide_side(target);

                player_pawn p = target as player_pawn;
                physical_object po = target as physical_object;

                if (p != null && p.get_is_dashing())
                {
                    dx = p.max_dx * Math.Sign(cx - p.cx) * 3.0f;
                }
                else if (po != null)
                {
                    // target is the thing running into us.
                    dx = po.dx * bounce_friction;
                }
            }

        }

        public class repeating_sprite : sprite
        {
            public repeating_sprite(float x, float y, int width_tiles, int height_tiles, int starting_sprite_id, int bank, int num_frames, int ticks_per_frame)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=ticks_per_frame,//how long is each frame shown.
                            //frames = new int[][]
                            //{
                            //    // TODO: Support animation.
                            //   new int[(width_tiles * height_tiles)]
                            //}
                        }
                    },
                };

                anims["default"].frames = new int[num_frames][];

                for (int frame = 0; frame < num_frames; frame++)
                {
                    anims["default"].frames[frame] = new int[(width_tiles * height_tiles)];

                    int frame_starting_sprite_id = starting_sprite_id + (frame * 2);

                    // TODO: Pass in tile information.
                    for (int i = 0; i < anims["default"].frames[frame].Length; i += 2)
                    {
                        bool top = (i / width_tiles) % 2 == 0;

                        if (top)
                        {
                            anims["default"].frames[frame][i] = frame_starting_sprite_id;
                            anims["default"].frames[frame][i + 1] = frame_starting_sprite_id + 1;
                        }
                        else
                        {
                            anims["default"].frames[frame][i] = frame_starting_sprite_id + 16;
                            anims["default"].frames[frame][i + 1] = frame_starting_sprite_id + 17;
                        }
                        //{ 864, 865, 864, 865, 864, 865, 864, 865, 880, 881, 880, 881, 880, 881, 880, 881, }
                    }
                }

                set_anim("default");

                w = width_tiles * 8;
                h = height_tiles * 8;
                cw = width_tiles * 8;
                ch = height_tiles * 8;

                this.x = x_initial = x;
                this.y = y_initial = y;

                this.bank = bank;
            }
        }

        public class platform : repeating_sprite
        {
            protected float tick_x = 0;
            protected float tick_y = 0;
            protected bool hit_this_frame = false;

            int ticks_per_dir_x;
            int ticks_per_dir_y;
            float linear_speed;

            float start_x;
            float end_x;
            float start_y;
            float end_y;

            // Number of frames to wait before moving the first time.
            public int start_delay;

            // Has this platform finished its start delay.
            bool activated;

            // In the case of a one_way platform, has it reached its destination.
            bool finished;

            // Stop after reach first destination
            public bool one_way;

            public enum movement_style
            {
                smooth = 0,
                linear = 1,
            }
            movement_style move_style;

            // Function used to move the platform based on the movement style.
            Func<float, float, float, float> MoveFunc;

            public platform(float x, float y, int width_tiles, int dist_tiles_x, int dist_tiles_y, movement_style move_style, int start_delay) 
                : base(x, y, width_tiles, 2, 864, 0, 1, 1)
            {
                linear_speed = 0.5f;
                ticks_per_dir_x = flr(abs((dist_tiles_x * 8.0f) / linear_speed));
                ticks_per_dir_y = flr(abs((dist_tiles_y * 8.0f) / linear_speed));


                start_x = x_initial;
                end_x = x_initial + dist_tiles_x * 8.0f;
                start_y = y_initial;
                end_y = y_initial + dist_tiles_y * 8.0f;

                this.move_style = move_style;

                this.start_delay = start_delay;
                if (start_delay == 0)
                {
                    activated = true;
                }
                else
                {
                    activated = false;
                }

                switch(move_style)
                {
                    case movement_style.linear:
                        {
                            MoveFunc = MathHelper.Lerp;
                            break;
                        }
                    case movement_style.smooth:
                        {
                            MoveFunc = MathHelper.SmoothStep;
                            break;
                        }
                }

                is_platform = true;
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                var self = this;

                hit_this_frame = false;

                // TODO: Error - This assumes a collision means you are standing on top!
                var touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                var touching_player_head = inst.intersects_box_box(
                    inst.pc.pawn.cx, inst.pc.pawn.cy - inst.pc.pawn.ch * 0.5f, 
                    inst.pc.pawn.cw * 0.5f, 1, 
                    self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p.pawn);

                var old_x = self.x;

                var old_y = self.y;

                //base._update60();

                if (!activated)
                {
                    // Just using tick_x since neither x nor y will matter until it is activated.
                    if (tick_x < start_delay)
                    {
                        tick_x++;
                    }
                    else
                    {
                        activated = true;
                        tick_x = 0;
                    }
                }

                if (activated && !finished)
                {
                    if (start_x != end_x)
                    {
                        x = MoveFunc(start_x, end_x, (float)tick_x / (float)ticks_per_dir_x);
                    }

                    if (tick_x >= ticks_per_dir_x)
                    {
                        if (one_way && start_x != end_x)
                        {
                            finished = true;
                        }
                        tick_x = 0;
                        float temp = start_x;
                        start_x = end_x;
                        end_x = temp;
                    }

                    if (start_y != end_y)
                    {
                        y = MoveFunc(start_y, end_y, (float)tick_y / (float)ticks_per_dir_y);
                    }

                    if (tick_y >= ticks_per_dir_y)
                    {
                        if (one_way && start_y != end_y)
                        {
                            finished = true;
                        }
                        tick_y = 0;
                        float temp = start_y;
                        start_y = end_y;
                        end_y = temp;
                    }

                    tick_x += 1;
                    tick_y += 1;
                }

                if (inst.cur_game_state != game_state.gameplay_dead)
                {
                    if (touching_player)
                    {
                        hit_this_frame = true;
                        inst.pc.pawn.x += self.x - old_x;
                        inst.pc.pawn.y += self.y - old_y;

                        // If the player is stand on the platform, and it's moving up, check if they are
                        // hitting a roof, and if so MURDER THEM!!!
                        if (end_y < start_y) // moving up
                        {
                            if (inst.collide_roof(inst.pc.pawn))
                            {
                                inst.pc.pawn.adjust_hp(-9999);
                            }
                        }
                    }
                    else
                    {
                        // Similar to above, if the platform is moving down and it pushes the player
                        // into the floor, MURDER THEM!!!!!!!!
                        if (end_y > start_y && touching_player_head) // moving down
                        {
                            if (inst.collide_floor(inst.pc.pawn))
                            {
                                inst.pc.pawn.adjust_hp(-9999);
                            }
                        }
                        inst.pc.pawn.platformed = false;
                    }
                }
            }
        }

        public class geyser : sprite
        {
            float dir_x;
            float dir_y;

            int sprite_id;

            public geyser(float x, float y, int w, int h, float dir_x, float dir_y) : base()
            {
                this.x = x;
                this.y = y;
                this.w = w;
                this.cw = w;
                this.h = h;
                this.ch = h;

                this.dir_x = dir_x;
                this.dir_y = dir_y;

                if (dir_x == 0)
                {
                    sprite_id = 704;

                    if (dir_y < 0)
                    {
                        flipy = true;
                    }
                }
                else
                {
                    sprite_id = 736;

                    if (dir_x < 0)
                    {
                        flipx = true;
                    }
                }
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                var touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, cx, cy, cw * 0.5f, (ch + 2) * 0.5f);

                if (touching_player)
                {
                    // Can't use dx for x direction because it gets zero'd out if you are not moving.
                    inst.pc.pawn.x += (dir_x * 4.0f); //(dir_x * 0.3f);
                    inst.pc.pawn.dy += (dir_y * 0.3f);
                }

                base._update60();
            }

            public override void _draw()
            {
                sprfxset(2, true);
                int tile_w = flr(w / 8.0f);
                int tile_h = flr(h / 8.0f);

                float start_x = x - w * 0.5f;
                float start_y = y - h * 0.5f;

                Point meta_start_pos = inst.map_pos_to_meta_tile(flr(start_x / 8.0f), flr(start_y / 8.0f));

                int sprite_offset = flr((inst.time_in_state % 15) / 5.0f) * 2;

                for (int i =0; i < tile_w; i+=2)
                {
                    for(int j = 0; j < tile_h; j+=2)
                    {
                        spr(sprite_id + sprite_offset, (meta_start_pos.X + i) * 8.0f, (meta_start_pos.Y + j) * 8.0f, 2, 2, flipx, flipy);
                    }
                }

                //rect(x - w * 0.5f, y - h * 0.5f, x + w * 0.5f, y + h * 0.5f, 0);
                //base._draw();
                sprfxset(2, false);
            }
        }

        public class player_top : player_pawn
        {
            public float dest_x = 0;
            public float dest_y = 0;

            public float walk_speed = 1.0f;

            public Vector2 desired_dir = Vector2.Zero;

            public bool is_flying = false;

            public player_top()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "walk_down",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(576, 2, 2),
                                create_anim_frame(578, 2, 2),
                                //create_anim_frame(580, 2, 2),
                                //create_anim_frame(578, 2, 2),
                            }
                        }
                    },
                    {
                        "walk_left",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(672, 2, 2),
                                create_anim_frame(674, 2, 2),
                                //create_anim_frame(676, 2, 2),
                                //create_anim_frame(674, 2, 2),
                            }
                        }
                    },
                    {
                        "walk_up",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(640, 2, 2),
                                create_anim_frame(642, 2, 2),
                                //create_anim_frame(644, 2, 2),
                                //create_anim_frame(642, 2, 2),
                            }
                        }
                    },
                    {
                        "walk_right",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(608, 2, 2),
                                create_anim_frame(610, 2, 2),
                                //create_anim_frame(612, 2, 2),
                                //create_anim_frame(610, 2, 2),
                            }
                        }
                    },

                    {
                        "idle_down",
                        new anim()
                        {
                            loop = true,
                            ticks=60,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                //create_anim_frame(576, 2, 2),
                                //create_anim_frame(578, 2, 2),
                                create_anim_frame(580, 2, 2),
                                create_anim_frame(582, 2, 2),
                            }
                        }
                    },
                    {
                        "idle_left",
                        new anim()
                        {
                            loop = true,
                            ticks=60,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                //create_anim_frame(672, 2, 2),
                                //create_anim_frame(674, 2, 2),
                                create_anim_frame(676, 2, 2),
                                create_anim_frame(678, 2, 2),
                            }
                        }
                    },
                    {
                        "idle_up",
                        new anim()
                        {
                            loop = true,
                            ticks=60,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                //create_anim_frame(640, 2, 2),
                                //create_anim_frame(642, 2, 2),
                                create_anim_frame(644, 2, 2),
                                create_anim_frame(646, 2, 2),
                                //create_anim_frame(648, 2, 2),
                            }
                        }
                    },
                    {
                        "idle_right",
                        new anim()
                        {
                            loop = true,
                            ticks=60,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                //create_anim_frame(608, 2, 2),
                                //create_anim_frame(610, 2, 2),
                                create_anim_frame(612, 2, 2),
                                create_anim_frame(614, 2, 2),
                                //create_anim_frame(616, 2, 2),
                            }
                        }
                    },
                };

                set_anim("idle_down");

                w = 16;
                h = 16;

                event_on_anim_done = null;
            }

            string next_idle = "down";

            public virtual bool on_reached_destination()
            {
                return false;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.hit_pause.is_paused() || (inst.cur_game_state == game_state.gameplay_dead) || controller == null)
                {
                    return;
                }

                // Calculate prior to moving so that if we reach the
                // destination, we still remember what direction we
                // we going.

                if (dest_x != x)
                {
                    float delta = dest_x - x;
                    float dir = Math.Sign(delta);
                    x += dir * min(walk_speed, abs(delta));
                    if (dir > 0)
                    {
                        set_anim("walk_right");
                        next_idle = "right";
                    }
                    else
                    {
                        set_anim("walk_left");
                        next_idle = "left";
                    }
                }
                if (dest_y != y)
                {
                    float delta = dest_y - y;
                    float dir = Math.Sign(delta);
                    y += dir * min(walk_speed, abs(delta));
                    if (dir > 0)
                    {
                        set_anim("walk_down");
                        next_idle = "down";
                    }
                    else
                    {
                        set_anim("walk_up");
                        next_idle = "up";
                    }
                }

                if (dest_x == x & dest_y == y && !btn(0) && !btn(1) && !btn(2) && !btn(3))
                {
                    set_anim("idle_" + next_idle);
                }

                //// Save the overworld position every time we land on a tile.
                //// TODO: This would ideally only be done when transitioning into a new level.
                ////       Or maybe it only needs to be done when entering an OW level?
                //if (dest_x == x && dest_y == y)
                //{
                //    Int32 packed_pos = flr(x / 8.0f) | (flr(y / 8.0f) << 16);
                //    dset((int)cartdata_index.overworld_pos_packed, packed_pos);
                //}


                const float tile_size = 16;

                // Arrived at tile and not being handled by derived class.
                if (dest_x == x && dest_y == y && !on_reached_destination())
                {
                    bool new_dest = false;

                    if (btn(0) && !new_dest)
                    {
                        dest_x -= tile_size;

                        bool going_off_screen = !inst.game_cam.is_pos_in_play_area(dest_x, dest_y);
                        // Are we trying to walk into a wall?
                        if (going_off_screen || (!is_flying && mfget(flr(dest_x / 8), flr(dest_y / 8), 0, 1) && !controller.DEBUG_fly_enabled))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                    if (btn(1) && !new_dest)
                    {
                        dest_x += tile_size;
                        bool going_off_screen = !inst.game_cam.is_pos_in_play_area(dest_x, dest_y);
                        // Are we trying to walk into a wall?
                        if (going_off_screen || (!is_flying && mfget(flr(dest_x / 8), flr(dest_y / 8), 0, 1) && !controller.DEBUG_fly_enabled))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                    if (btn(2) && !new_dest)
                    {
                        dest_y -= tile_size;
                        bool going_off_screen = !inst.game_cam.is_pos_in_play_area(dest_x, dest_y);
                        // Are we trying to walk into a wall?
                        if (going_off_screen || (!is_flying && mfget(flr(dest_x / 8), flr(dest_y / 8), 0, 1) && !controller.DEBUG_fly_enabled))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                    if (btn(3) && !new_dest)
                    {
                        dest_y += tile_size;
                        bool going_off_screen = !inst.game_cam.is_pos_in_play_area(dest_x, dest_y);
                        // Are we trying to walk into a wall?
                        if (going_off_screen || (!is_flying && mfget(flr(dest_x / 8), flr(dest_y / 8), 0, 1) && !controller.DEBUG_fly_enabled))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                }
            }
        }

        public class player_controller : sprite
        {
            public player_pawn pawn { get; protected set; }

            public artifacts found_artifacts = artifacts.none;// artifacts.health_00 | artifacts.dash_pack | artifacts.jump_boots;

            // Iinitial implementation supports 8 levels with up to 4 gems on 
            // each.
            // TODO: Add ability to dynamically calculate how many gems are in game.
            // TODO: Expand to support more levels.
            // TODO: Possibly expand the support variable numbers of gems per level.
            public UInt32 found_gems_00;
            public UInt32 found_gems_01;

            public bool DEBUG_fly_enabled = false;
            public bool DEBUG_god_enabled = false;

            public player_controller()
            {
                reload();
            }

            public void reload()
            {
                found_gems_00 = (uint)dget((uint)Game1.cartdata_index.gems_00);
                found_gems_01 = (uint)dget((uint)Game1.cartdata_index.gems_01);
                found_artifacts = (artifacts)dget((uint)Game1.cartdata_index.artifacts);
            }

            public override void _preupdate()
            {
                base._preupdate();

                pawn?._preupdate();
            }

            public override void _update60()
            {
                base._update60();

                pawn?._update60();
            }

            public override void _postupdate()
            {
                base._postupdate();

                pawn?._postupdate();
            }

            public override void _draw()
            {
                base._draw();

                pawn?.push_pal();
                pawn?._draw();
                pawn?.pop_pal();
            }

            public virtual void possess(player_pawn p)
            {
                p.set_controller(this);

                // Copy over important stuff between the pawns.
                if (pawn != null && pawn.hp > 0)
                {
                    p.hp = pawn.hp;
                    //p.curanim = pawn.curanim;
                    //p.curframe = pawn.curframe;
                    //p.flipx = pawn.flipx;
                    //p.dx = pawn.dx;
                    //p.dy = pawn.dy;

                    // If the new pawn is the same type as the old type, then re-use it
                    // as this is just a simple level transition.
                    if (pawn != null && p.GetType() == pawn.GetType())
                    {
                        pawn.x = p.x;
                        pawn.y = p.y;
                    }
                    else
                    {
                        pawn.set_controller(null);
                        pawn = p;
                    }
                }
                else
                {
                    // if this is the first pawn, give them full health.
                    // or if the current pawn is dead (we assume this means we are respawning).
                    p.hp = p.get_hp_max();
                    pawn = p;
                }
            }
            
            public bool has_artifact(artifacts artifact)
            {
                return (found_artifacts & artifact) != 0;
            }

            public int get_gem_count()
            {
                int gem_count = 0;

                gem_count += get_gem_count_in_group(found_gems_00);
                gem_count += get_gem_count_in_group(found_gems_01);

                return gem_count;
            }

            private int get_gem_count_in_group(uint gems_found_cache)
            {
                int gem_count = 0;

                //printh("get_gem_count: " + Convert.ToString(gems_found_cache, 2));

                // Loop through each bit to see which contain a found gem.
                for (int i = 0; i < 32; i++)
                {
                    gem_count += (int)(gems_found_cache & 1);
                    gems_found_cache = gems_found_cache >> 1;
                }
                return gem_count;
            }

            public bool is_gem_found(int id)
            {

                // Store prior to modding value.
                int cached_id = id;
                id %= 32;

                UInt32 gem_mask = (UInt32)1 << id;// + (inst.gems_per_level * inst.cur_level_id);

                if (cached_id <= 31)
                {
                    return (inst.pc.found_gems_00 & gem_mask) != 0;
                }
                else
                {
                    return (inst.pc.found_gems_01 & gem_mask) != 0;
                }
            }
        }

        [Flags]
        public enum artifacts : Int32
        {
            none = 0,

            // Health pieces
            health_00 = 1 << 0,
            health_01 = 1 << 1,
            health_02 = 1 << 2,
            health_03 = 1 << 3,
            health_04 = 1 << 4,
            health_start = health_00,
            health_end = health_04, // UPDATE TECH BELOW WHEN ADDING HEALTH

            // Human tech
            dash_pack = 1 << 5,
            jump_boots = 1 << 6,
            rock_smasher = 1 << 7,
            ground_slam = 1 << 8,
            light = 1 << 9,
            air_tank = 1 << 10,

            // TODO: Re-breather for underwater
            // TODO: Light for caves

            MAX = 1 << 31, // Just here as a reminder that this bitmask must remain 32 bit.

            // Alien relics
        }

        // A list of mututially exclusive tile flags. Because they are mutually exclusive we have
        // have overlay between their bits.
        public enum packed_tile_types
        {
            spike = 1,
            vanishing = 2,
            arc = 3,
            water = 4,
            rock_smash = 5,
            pass_through = 6,
            dissolving = 7,
            no_bouce = 8,
        }

        // Returns a packed_tile_types id.
        public int unpack_tile_flags(int tile_flags)
        {
            // get the raw tile flags.
            // shift 1 to the left to eliminate the first bit ("wall").
            // Isolate the next 4 bits (note: currently only using 3) which
            // contain the packed tile id.
            return (tile_flags >> 1) & 0xff;
        }

        public bool is_packed_tile(int tile_flags, packed_tile_types type)
        {
            return unpack_tile_flags(tile_flags) == (int)type;
        }

        public class player_pawn : sprite
        {
            protected player_controller controller;

            public bool platformed = false;
            public float max_dx = 1.5f;//max x speed
            public float max_dy = 5;//max y speed

            public bool supports_map_links = true;

            // Hack to solve case where player hits 2 flying enemies in the same frame.
            // First enemy starts playing dash bouncing, and second sees he isn't jumping
            // and does damage.
            // Note: I think a better, more general solution, would be to make player invul
            //       for a short time after dash bouncing, or possibly even let them attack
            //       during that time.
            public bool dashing_last_frame { get; protected set; }

            public override int get_hp_max()
            {
                int health = 1;

                for (int i = (int)artifacts.health_start; i <= (int)artifacts.health_end; i = i << 1)
                {
                    if ((controller.found_artifacts & (artifacts)i) != 0)
                    {
                        health++;
                    }
                }
                return health;
            }

            public virtual bool get_is_dashing()
            {
                return false;
            }

            public virtual void start_dash_bounce(ref Vector2 hit_point)
            {
            }

            public virtual void on_take_hit(sprite attacker)
            {

            }

            public virtual void adjust_hp(float damage_amount)
            {

            }

            // Should only be called by PC, so that relationship is maintained.
            public virtual void set_controller(player_controller c)
            {
                controller = c;
            }
        }

        //make the player
        public class player_side : player_pawn
        {
            //todo: refactor with m_vec.

            public float jump_speed = -2.5f;//jump veloclity
            public float acc = 1.0f;//0.15f;//acceleration
            public float dcc = 0.0f;//decceleration
            public float air_dcc = 0.95f;//air decceleration
            public float grav = 0.18f;

            public float dash_time = 0;
            public int dash_count = 0;

            // count down used for continuing to draw trails after dash is complete. This only
            // drains when the player is grounded, etc.
            public float trail_time = 0;

            complex_button jump_button = new complex_button(4);
            complex_button dash_button = new complex_button(5);

            int max_jump_press = 12;//max time jump can be held

            int jump_count = 0;

            bool in_water = false;
            int in_water_timer = 0;
            int time_before_water_damage_start = 240;
            const int time_between_water_damage = 60;

            // The direction of the current dash. If not dashing,
            // this will be zero.
            Vector2 dash_dir = Vector2.Zero;

            Queue<sprite> player_hist = new Queue<sprite>(60);
            bool is_hist = false;

            Dictionary<Point, float> dissolve_tracker = new Dictionary<Point, float>();

            public player_side() : base()
            {
                //animation definitions.
                //use with set_anim()
                anims = new Dictionary<string, anim>()
                {
                    {
                        "stand",
                        new anim()
                        {
                            ticks=20,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(0, 4, 3),
                                create_anim_frame(4, 4, 3),
                                create_anim_frame(8, 4, 3),
                                create_anim_frame(124, 4, 3),
                            }
                        }
                    },
                    {
                        "walk",
                        new anim()
                        {
                            loop = true,
                            ticks=7,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35, 48, 49, 50, 51 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(12, 4, 3),
                                create_anim_frame(48, 4, 3),
                                create_anim_frame(52, 4, 3),
                                create_anim_frame(48, 4, 3),
                            }
                        }
                    },
                    {
                        "jump",
                        new anim()
                        {
                            h = 32+8,
                            ticks=1,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(56-16, 4, 5, 1),
                            },//what frames are shown.
                        }
                    },
                    {
                        "fall",
                        new anim()
                        {
                            h = 32+8,
                            ticks=1,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(256-16, 4, 5, 1),
                            },//what frames are shown.
                        }
                    },
                    {
                        "slide",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35, 48, 49, 50, 51 } },//what frames are shown.
                        }
                    },
                    {
                        "dash",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            //frames= new int[][] { create_anim_frame(60-16, 4, 5, 1), },//what frames are shown.
                            frames= new int[][]
                            {
                                //create_anim_frame(160, 4, 3),
                                //create_anim_frame(164, 4, 3),
                                create_anim_frame(60, 4, 3),
                            },//what frames are shown.
                        }
                    },
                    {
                        "dash_air",
                        new anim()
                        {
                            //h = 40,
                            //ticks=5,//how long is each frame shown.
                            //frames= new int[][] { create_anim_frame(60-16, 4, 5, 1), },
                            ticks=5,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(160, 4, 3),
                                create_anim_frame(164, 4, 3),
                                //create_anim_frame(168, 4, 3),
                            },//what frames are shown.
                        }
                    },
                    {
                        "dash_down",
                        new anim()
                        {
                            h =32,
                            ticks=5,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(332, 4, 4),
                                create_anim_frame(396, 4, 4),
                            },
                        }
                    },
                    {
                        "taking_hit",
                        new anim()
                        {
                            h = 32,
                            ticks=1,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(172, 4, 4),
                            },//what frames are shown.
                        }
                    },
                };

                x = 159;
                y = 932 - 32 * 8;
                dx = 0;
                dy = 0;
                w = 32;
                h = 24;

                cw = 16;
                ch = 24; // 16;
                //cx_offset = 8;
                //cy_offset = 3;
            }

            public override void start_dash_bounce(ref Vector2 hit_point)
            {
                // Don't allow dash bounce when sliding under.
                // This is more properly handled prior to call start_dash_bounce in
                // some cases, but this handles stuff like the case so dashing through
                // a flying enemy.
                if (is_sliding_under())
                {
                    return;
                }
                // Clear out the dash direction since we hit a surface of something.
                dash_dir = Vector2.Zero;

                dx = 5 * -Math.Sign(hit_point.X - cx);
                dash_time = 0;

                int mx = flr(hit_point.X / 8.0f);
                int my = flr(hit_point.Y / 8.0f);

                if (inst.is_packed_tile(fget(mget_tiledata(mx, my)), packed_tile_types.no_bouce))
                {
                    Point map_point = inst.map_pos_to_meta_tile(mx, my);
                    inst.objs_add_queue.Add(new simple_fx_tile_glimmer() { x = map_point.X * 8 + 8, y = map_point.Y * 8 + 8});
                    return;
                }

                // Only do this stuff when bouncing up.
                dash_count = 0;
                dy = -max_dy;
                inst.objs_add_queue.Add(new simple_fx() { x = hit_point.X, y = y + h * 0.25f });

                if (inst.is_packed_tile(fget(mget_tiledata(mx, my)), packed_tile_types.vanishing))
                {
                    inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 }, 0);
                    inst.objs_add_queue.Add(new block_restorer(mx, my, 240, packed_tile_types.vanishing));
                }
                if (inst.is_packed_tile(fget(mget_tiledata(mx, my)), packed_tile_types.arc))
                {
                    //inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 }, 0);
                    Point map_point = inst.map_pos_to_meta_tile(mx, my);
                    inst.objs_add_queue.Add(new rock_pendulum(Math.Sign(hit_point.X - x)) { x = map_point.X * 8 + 8, y = map_point.Y * 8 + 8 });
                }
                if (inst.is_packed_tile(fget(mget_tiledata(mx, my)), packed_tile_types.rock_smash) && inst.pc.has_artifact(artifacts.rock_smasher))
                {
                    inst.smash_rock(mx, my, true);
                }

                inst.hit_pause.start_pause(hit_pause_manager.pause_reason.bounce);
            }

            public override bool get_is_dashing()
            {
                return dash_time > 0;
            }

            public override void _preupdate()
            {
                base._preupdate();

                if (inst.hit_pause.is_paused() || (inst.cur_game_state == game_state.gameplay_dead))
                {
                    return;
                }

                if (invul_time > (invul_time_on_hit * 0.6f))
                {
                    return;
                }

                if (dash_time > 0)
                {
                    dashing_last_frame = true;
                }
                else
                {
                    dashing_last_frame = false;
                }
            }

            private bool is_sliding_under()
            {
                bool hit_box = false;
                foreach(var o in inst.objs)
                {
                    physical_object p = o as physical_object;
                    if (p != null)
                    {
                        if (inst.intersects_obj_box(p, cx, cy - 16, cw * 0.5f, ch * 0.5f))
                        {
                            hit_box = true;
                            break;
                        }
                    }
                }
                return grounded != 0 && (mfget(flr(cx / 8), flr(cy / 8) - 2, 0) || hit_box) && dash_time > 0;
            }

            //call once per tick.
            public override void _update60()
            {
                if (inst.hit_pause.is_paused() || (inst.cur_game_state == game_state.gameplay_dead))
                {
                    return;
                }

                var self = this;

                string next_anim = curanim;

                if (invul_time > (invul_time_on_hit * 0.6f))
                {
                    x += dx;
                    set_anim("taking_hit");
                    base._update60();
                    return;
                }

                //if( inst.collide_floor(self))
                //{
                //    printh("hit floor");
                //}

                //todo: kill enemies.

                //track button presses
                var bl = btn(0); //left
                var br = btn(1); //right
                var bu = btn(2); //up
                var bd = btn(3); //down
                dash_button._update60();

                const float dash_speed = 3.0f;

                if (dash_button.is_pressed && dash_count == 0 && dash_time <= 0 && inst.pc.has_artifact(artifacts.dash_pack))
                {
                    dash_count = 1;
                    
                    // Try to make dash go exactly 4 meta tiles. Note that if dash_speed is greater than
                    // 1, it will not land exactly on point as we always move the full dash_speed every frame.
                    dash_time = ((4.0f * 16.0f) / dash_speed);
                    // NOTE: only drains when grounded, etc.
                    trail_time = 15;
                    dy = 0;
                    self.jump_hold_time = 0; // kill jump

                    // Assume not direction to start.
                    dash_dir = Vector2.Zero;

                    // First check up and down to let that take
                    // presendence, since that feels better.
                    //if (bu)
                    //{
                    //    dash_dir.Y = -1;
                    //    // Clear the x momentum.
                    //    dx = 0;
                    //}
                    //else 
                    if (bd && inst.pc.has_artifact(artifacts.ground_slam))
                    {
                        dash_dir.Y = 1;
                        dx = 0;
                    }
                    else if (br)
                    {
                        self.flipx = false;
                        dash_dir.X = 1;
                    }
                    else if (bl)
                    {
                        self.flipx = true;
                        dash_dir.X = -1;
                    }
                    else
                    {
                        // If no direction is being pressed but we are starting a
                        // dash, fall back to the currently looking direction of the
                        // player.
                        if (self.flipx)
                        {
                            dash_dir.X = -1;
                        }
                        else
                        {
                            dash_dir.X = 1;
                        }
                    }
                }

                bool is_dashing = get_is_dashing();

                //move left/right
                if (!is_dashing)
                {
                    if (controller.DEBUG_fly_enabled)
                    {
                        if (bl == true)
                        {
                            self.dx -= self.acc;
                            br = false;//handle double press
                        }
                        else if (br == true)
                        {
                            self.dx += self.acc;
                        }
                        else
                        {
                            self.dx *= self.dcc;
                        }
                        if (bu == true)
                        {
                            self.dy -= self.acc;
                            br = false;//handle double press
                        }
                        else if (bd == true)
                        {
                            self.dy += self.acc;
                        }
                        else
                        {
                            self.dy *= self.dcc;
                        }
                    }
                    else
                    {
                        if (bl == true)
                        {
                            self.dx -= self.acc;
                            br = false;//handle double press
                        }
                        else if (br == true)
                        {
                            self.dx += self.acc;
                        }
                        else
                        {
                            if (!platformed)
                            {
                                if (self.grounded != 0)
                                {

                                    self.dx *= self.dcc;
                                }
                                else
                                {
                                    self.dx *= self.air_dcc;
                                }
                            }
                        }
                    }
                }

                // No expiry for down dash. It feels better this way since
                // falling 
                if (dash_dir.Y <= 0)
                {
                    // While sliding under solid objects, don't let the dash end.
                    if (is_sliding_under())
                    {
                        dash_time = max(1, dash_time - 1);
                    }
                    else
                    {
                        dash_time = max(0, dash_time - 1);
                    }

                    // If the player is also grounded and not dashing, start to remove the dashing trail.
                    if (grounded != 0 && !get_is_dashing())
                    {
                        trail_time = max(0, trail_time - 1);
                    }
                }

                if (is_dashing)
                {
                    if (dash_dir.X < 0)
                    {
                        dx = -dash_speed;
                    }
                    else if (dash_dir.X > 0)
                    {
                        dx = dash_speed;
                    }
                    if (dash_dir.Y < 0)
                    {
                        dy = -dash_speed;
                    }
                    else if (dash_dir.Y > 0)
                    {
                        // ground pound is faster.
                        dy = dash_speed * 4;
                    }
                }
                else if (!platformed && !controller.DEBUG_fly_enabled)
                {
                    //limit walk speed
                    self.dx = mid(-self.max_dx, self.dx, self.max_dx);
                }
                //else
                //{
                //    self.dx = mid(-dash_speed, self.dx, dash_speed);
                //}

                //move in x
                self.x += self.dx;

                if (inst.cur_map_config.is_tower)
                {
                    self.x = (self.x + inst.cur_map_config.map_size_pixels.X) % inst.cur_map_config.map_size_pixels.X;
                }

                ch = 24;
                cy_offset = 0;

                if (is_dashing)
                {
                    if (grounded != 0)
                    {
                        ch = 16;
                        cy_offset = 4;
                    }
                }

                //hit walls
                float old_dx = dx;
                Vector2 hit_point = new Vector2();
                if (!controller.DEBUG_fly_enabled && inst.collide_side(self, out hit_point))
                {
                    if (is_dashing && dash_dir.X != 0)
                    {
                        if (!is_sliding_under())
                        {
                            start_dash_bounce(ref hit_point);
                            is_dashing = false;
                        }
                        else
                        {
                            dash_dir.X *= -1; // dx = -1 * old_dx;
                            dx = -1 * old_dx;
                            flipx = !flipx;
                        }
                    }

                }

                //jump buttons
                self.jump_button._update60();

                bool in_water_new = (inst.is_packed_tile(fget(mget_tiledata(flr(x / 8), flr(y / 8))), packed_tile_types.water));
                bool head_in_water_new = (inst.is_packed_tile(fget(mget_tiledata(flr(x / 8), flr(y / 8) - 1)), packed_tile_types.water));

                if (!in_water_new || controller.has_artifact(artifacts.air_tank))
                {
                    in_water_timer = 0;
                }

                if (!in_water && in_water_new)
                {
                    inst.objs.Add(new water_splash()
                    {
                        x = x,
                        y = flr(y / 16) * 16.0f - 8.0f,
                    });
                }
                if (head_in_water_new && !controller.has_artifact(artifacts.air_tank))
                {
                    in_water_timer++;
                    if (in_water_timer > time_before_water_damage_start)
                    {
                        if ((in_water_timer - time_before_water_damage_start) % time_between_water_damage == 0)
                        {
                            invul_time = 10;

                            adjust_hp(-0.25f);
                        }
                    }
                }

                in_water = in_water_new;

                //jump is complex.
                //we allow jump if:
                //	on ground
                //	recently on ground
                //	pressed btn right before landing
                //also, jump velocity is
                //not instant. it applies over
                //multiple frames.
                //if (!is_dashing)

                // Check dy for the case when the player is landing in water. We don't want the max hold to suddenly jump up
                // because that will trigger them to start moving up again, even if they are already falling.
                int mod_max_jump_press = in_water && dy < 0 ? max_jump_press * 4 : max_jump_press;
                float mod_jump_speed = in_water ? jump_speed * 1.0f : jump_speed;


                {
                    if (self.jump_button.is_down && !is_sliding_under())
                    {
                        //is player on ground recently.
                        //allow for jump right after 
                        //walking off ledge.
                        var on_ground = (self.grounded != 0 || self.airtime < 5);
                        //was btn presses recently?
                        //allow for pressing right before
                        //hitting ground.
                        var new_jump_btn = self.jump_button.ticks_down < 10;

                        int max_jump_count = (controller.has_artifact(artifacts.jump_boots)) ? 2 : 1;
                        //is player continuing a jump
                        //or starting a new one?
                        if (self.jump_hold_time > 0)
                        {
                            self.jump_hold_time += 1;
                            //keep applying jump velocity
                            //until max jump time.
                            if (self.jump_hold_time < mod_max_jump_press)
                            {
                                self.dy = mod_jump_speed;//keep going up while held
                            }

                            dash_time = 0;
                            is_dashing = false;
                        }
                        else if ((on_ground && new_jump_btn && jump_count == 0))
                        {
                            // Are we jumping off a pass through floor with "down" held?
                            if (inst.is_packed_tile(grounded, packed_tile_types.pass_through) && btn(3))
                            {
                                // Teleport below the floor to avoid collision.
                                y += 8;

                                // Artifically increment airtime to avoid hitting 
                                // "late jump" code in "on_ground" calculation above here.
                                self.airtime = 5;
                            }
                            else
                            {
                                jump_hold_time += 1;
                                jump_count++;
                                dash_time = 0;
                                is_dashing = false;
                                self.dy = mod_jump_speed;
                            }
                        }
                        else if (jump_count < max_jump_count && is_dashing && jump_button.is_pressed)
                        {
                            // Additional jumps get a little effect to show that it is special
                            inst.objs_add_queue.Add(new simple_fx() { x = x, y = y + h * 0.5f });
                            jump_hold_time += 1;
                            jump_count++;
                            dash_time = 0;
                            is_dashing = false;
                            self.dy = mod_jump_speed;

                            // Starting a dash out, so dash is cancelled.
                            dash_dir = Vector2.Zero;
                        }
                    }
                    else
                    {
                        if (jump_button.is_released && (self.jump_hold_time > 0 && self.jump_hold_time < mod_max_jump_press))
                        {
                            self.dy = -1.0f;
                        }

                        self.jump_hold_time = 0;
                    }

                    float grav_mod = in_water ? 0.5f : 1.0f;

                    //move in y
                    if (dash_dir.Y == 0 && !controller.DEBUG_fly_enabled)
                    {
                        self.dy += self.grav * grav_mod;
                    }
                }

                self.dy = mid(-self.max_dy, self.dy, self.max_dy);

                // Apply gravity if not dashing, or vertical dash.
                if (!is_dashing || dash_dir.Y != 0 || controller.DEBUG_fly_enabled) // re-eval is_dashing since we might have just started jumping.
                {
                    self.y += self.dy;
                }
                else if (dash_dir.Y == 0) // is horizontal dashing.
                {
                    self.dy = 0; // kill building pull down
                }

                //floor
                if (controller.DEBUG_fly_enabled || !inst.collide_floor(self, out hit_point))
                {
                    if (dy < 0)
                    {
                        next_anim = ("jump");
                    }
                    else
                    {
                        next_anim = "fall";
                    }

                    self.grounded = 0;
                    self.airtime += 1;
                    if (self.airtime >= 5 && jump_count == 0)
                    {
                        // We fell of a ledge. eat a jump so that you can't fall->jump->jump to get further.
                        jump_count++;
                    }
                }
                else
                {
                    if (inst.is_packed_tile(grounded, packed_tile_types.dissolving))
                    {
                        Point meta_pos = inst.map_pos_to_meta_tile(flr(hit_point.X / 8), flr(hit_point.Y / 8));
                        if (dissolve_tracker.ContainsKey(meta_pos))
                        {
                            dissolve_tracker[meta_pos]++;
                        }
                        else
                        {
                            dissolve_tracker.Add(meta_pos, 1);
                        }

                        if (inst.time_in_state % 10 == 0)
                        {
                            inst.objs_add_queue.Add(new simple_fx_dust() { x = hit_point.X + rnd(4), y = hit_point.Y + rnd(4) });
                        }

                        if (dissolve_tracker[meta_pos] > 30)
                        {
                            inst.change_meta_tile(flr(hit_point.X / 8), flr(hit_point.Y / 8), new int[] { 836, 837, 852, 853 }, 0);
                            dissolve_tracker.Remove(meta_pos);
                            inst.objs_add_queue.Add(new block_restorer(meta_pos.X, meta_pos.Y, 240, packed_tile_types.dissolving));
                        }
                    }

                    jump_count = 0;

                    // Are we downward dashing?
                    if (dash_dir.Y > 0 & is_dashing)
                    {
                        inst.game_cam.shake(10, 3);
                        start_dash_bounce(ref hit_point);

                        // hack. Should probably be distance based.
                        foreach (var o in inst.objs)
                        {
                            if (o.GetType() == typeof(badguy))
                            {
                                if ((o as badguy).grounded != 0)
                                {
                                    (o as badguy).on_launch();
                                }
                            }
                        }
                    }
                }

                //roof
                if (!controller.DEBUG_fly_enabled && inst.collide_roof(self))
                {
                    // TODO: For now just kill dash. Should probably do some of 
                    // start_dash_bounce() to trigger tiles changes etc, but
                    // not the bouncing part.
                    dash_dir = Vector2.Zero;
                    dash_time = 0;
                    dy = 0;
                }

                Tuple<float, float>[] hit_tests = new Tuple<float, float>[]
                {
                    new Tuple<float, float>(-cw * 0.25f, 0),
                    new Tuple<float, float>(cw * 0.25f, 0),
                    new Tuple<float, float>(0, -ch * 0.25f),
                    new Tuple<float, float>(0, ch * 0.25f),
                };

                // Search for spike in towards the center of 4 edges of player.
                foreach (var h in hit_tests)
                {
                    int cell_x = flr(((cx + h.Item1) / 8));
                    int cell_y = flr(((cy + h.Item2) / 8));
                    if (inst.is_packed_tile(fget(mget_tiledata(cell_x, cell_y)), packed_tile_types.spike))
                    {
                        // placeholder to do massive damage.
                        sprite temp = new sprite()
                        {
                            attack_power = float.MaxValue,
                        };

                        on_take_hit(temp);

                        break;
                    }
                }

                //handle playing correct animation when
                //on the ground.
                if (self.grounded != 0 && !is_dashing)
                {
                    if (br)
                    {
                        if (self.dx < 0)
                        {
                            //pressing right but still moving left.
                            next_anim = ("slide");
                        }
                        else
                        {
                            next_anim = ("walk");
                        }
                    }
                    else if (bl)
                    {
                        if (self.dx > 0)
                        {
                            //pressing left but still moving right.
                            next_anim = ("slide");
                        }
                        else
                        {
                            next_anim = ("walk");
                        }

                    }
                    else
                    {
                        next_anim = ("stand");
                    }

                }
                
                if (is_dashing)
                {
                    if (grounded != 0)
                    {
                        if (is_sliding_under())
                        {
                            next_anim = ("dash");
                        }
                        else
                        {
                            next_anim = "dash_air";
                        }
                    }
                    else
                    {
                        if (dash_dir.Y != 0)
                        {
                            next_anim = "dash_down";
                        }
                        else
                        {
                            next_anim = "dash_air";
                        }
                    }
                }

                // Has the dash expired?
                if (dash_time <= 0)
                {
                    // We are no longer dashing in any direction.
                    dash_dir = Vector2.Zero;

                    // If we are also on the ground, reset the dash count, so
                    // the user can dash again.
                    if (grounded != 0)
                    {
                        dash_count = 0;
                    }
                }

                //flip
                if (!is_dashing)
                {
                    if (br)
                    {
                        self.flipx = false;
                    }
                    else if (bl)
                    {
                        self.flipx = true;
                    }
                }

                set_anim(next_anim);

                base._update60();
            }

            public override void _postupdate()
            {
                base._postupdate();
            }

            public override void adjust_hp(float damage_amount)
            {
                hp += damage_amount;

                if (hp <= 0)
                {
                    inst.set_game_state(game_state.gameplay_dead);
                    inst.hit_pause.start_pause(hit_pause_manager.pause_reason.death);

                    Tuple<float, float>[] death_dirs = new Tuple<float, float>[]
                    {
                        new Tuple<float, float>(-1.0f, 0.0f),
                        new Tuple<float, float>( 1.0f, 0.0f),
                        new Tuple<float, float>( 0.0f, 1.0f),
                        new Tuple<float, float>( 0.0f,-1.0f),
                        new Tuple<float, float>(-0.7f,-0.7f),
                        new Tuple<float, float>( 0.7f,-0.7f),
                        new Tuple<float, float>( 0.7f, 0.7f),
                        new Tuple<float, float>(-0.7f, 0.7f),
                    };

                    foreach (var dir in death_dirs)
                    {
                        simple_fx_death_spark o = new simple_fx_death_spark(dir.Item1, dir.Item2)
                        {
                            x = x,
                            y = y,
                        };
                        inst.objs_add_queue.Add(o);
                    }
                }
            }

            public override void on_take_hit(sprite attacker)
            {
                if (invul_time > 0)
                {
                    return;
                }
                
                invul_time = 120;
                dx = Math.Sign(cx - attacker.cx) * 0.25f;
                dy = 0;
                jump_hold_time = 0;

                if (!controller.DEBUG_god_enabled)
                {
                    adjust_hp(-attacker.attack_power);
                }
            }

            public override void push_pal()
            {
                if (inst.active_map_link?.trans_dir != map_link.transition_dir.none)
                {
                    // don't call super.
                    inst.apply_pal(inst.default_pal);
                }
                else
                {
                    base.push_pal();
                }
            }

            public override void _draw()
            {
                if (!is_hist)
                {
                    // The number of past players to draw.
                    int hist_len = 8;
                    // Only adjust the history every so often, so avoid to much overlap.
                    if (inst.time_in_state % 4 == 0)
                    {
                        {
                            player_side copy = MemberwiseClone() as player_side;
                            copy.is_hist = true;
                            player_hist.Enqueue(copy);
                        }

                        while (player_hist.Count > hist_len)
                        {
                            player_hist.Dequeue();
                        }

                        // If we no longer want to trail dequeue twice to account for the one just added
                        // and one more to start shrinking the queue. This is a little clumbsy but works.
                        if (trail_time <= 0)
                        {
                            if (player_hist.Count > 0)
                            {
                                player_hist.Dequeue();
                            }
                            if (player_hist.Count > 0)
                            {
                                player_hist.Dequeue();
                            }
                        }
                    }

                    //if (get_is_dashing())
                    {
                        float i = 3;
                        float step = (float)inst.bright_table.Length / (float)hist_len;
                        foreach (sprite s in player_hist)
                        {
                            //inst.apply_pal(inst.fade_table[3]);
                            inst.apply_pal(inst.bright_table[(int)Math.Ceiling(i)]);
                            (s)._draw();
                            i = max(0, (float)i - step);
                        }
                        pal();
                    }
                }

                if (inst.cur_game_state != game_state.gameplay_dead)
                {
                    if (in_water_timer > 0 /*&& in_water_timer < time_before_water_damage_start*/ && !is_hist)
                    {
                        float percent = max(1.0f - ((float)in_water_timer / (float)time_before_water_damage_start), 0.0f);

                        rectfill(x - 17, y - 17, x - 15 + (32), y - 15, 0);

                        // Line drawing will show a single pixel for a width of 0, so avoid that.
                        if (percent > 0)
                        {
                            line(x - 16, y - 16, x - 16 + (32 * percent), y - 16, 7);
                        }
                    }

                    if (!is_hist)
                    {
                        float old_x = x;
                        float old_y = y;
                        for (int i = -1; i < 2; i++)
                        {
                            for (int j = -1; j < 2; j++)
                            {
                                if (i != j)
                                {
                                    inst.apply_pal(inst.bright_table[3]);
                                    x = old_x + i;
                                    y = old_y + j;
                                    bool old = inst.debug_draw_enabled;
                                    inst.debug_draw_enabled = false;
                                    base._draw();
                                    inst.debug_draw_enabled = old;
                                }
                            }
                        }
                        x = old_x;
                        y = old_y;
                        pal();
                    }

                    base._draw();
                }
            }
        }

        // Object used to manage multiple cameras in a single level.
        public class cam_manager : PicoXObj
        {
            public List<cam> cameras = new List<cam>();

            public cam queued_cam;
            public int queued_ticks;

            public override void _update60()
            {
                base._update60();

                queued_ticks++;

                if (inst.pc.pawn != null)
                {
                    for (int i = 0; i < cameras.Count; i++)
                    {
                        cam c = cameras[i];
                        if (c != inst.game_cam && c != queued_cam)
                        {
                            if (c.is_pos_in_play_area(inst.pc.pawn.x, inst.pc.pawn.y))
                            {
                                queued_ticks = 0;
                                queued_cam = c;
                                c.jump_to_target();
                                // force an update to get it in the starting position.
                                c._update60();
                                break;
                            }
                        }
                    }
                }
            }
        }

        //make the camera.
        public class cam : PicoXObj
        {

            player_controller tar;//target to follow.
            Vector2 pos;

            //how far from center of screen target must
            //be before camera starts following.
            //allows for movement in center without camera
            //constantly moving.
            public float pull_threshold = 16;

            public Vector2 pull_threshold_offset = Vector2.Zero;

            //min and max positions of camera.
            //the edges of the level.
            public Vector2 pos_min = new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f);
            public Vector2 pos_max = new Vector2(368 - inst.Res.X * 0.5f, 1024 - inst.Res.Y * 0.5f);

            public Vector2 play_area_min = Vector2.Zero;
            public Vector2 play_area_max = Vector2.Zero;

            int shake_remaining = 0;
            float shake_force = 0;

            public cam(player_controller target)
            {
                tar = target;
                jump_to_target();
            }
            public void jump_to_target()
            {
                if (tar.pawn != null)
                {
                    pos = new Vector2(tar.pawn.x, tar.pawn.y);
                }
            }
            public override void _update60()
            {
                var self = this;

                base._update60();

                self.shake_remaining = (int)max(0, self.shake_remaining - 1);

                float max_cam_speed = 88888.0f;

                if (self.tar.pawn != null)
                {

                    //follow target outside of
                    //pull range.
                    if (pull_max_x() < self.tar.pawn.x)
                    {

                        self.pos.X += min(self.tar.pawn.x - pull_max_x(), max_cam_speed);

                    }
                    if (pull_min_x() > self.tar.pawn.x)
                    {
                        self.pos.X += min((self.tar.pawn.x - pull_min_x()), max_cam_speed);
                    }


                    if (pull_max_y() < self.tar.pawn.y)
                    {
                        self.pos.Y += min(self.tar.pawn.y - pull_max_y(), max_cam_speed);

                    }
                    if (pull_min_y() > self.tar.pawn.y)
                    {
                        self.pos.Y += min((self.tar.pawn.y - pull_min_y()), max_cam_speed);

                    }
                }

                //lock to edge
                if (self.pos.X < self.pos_min.X) self.pos.X = self.pos_min.X;
                if (self.pos.X > self.pos_max.X) self.pos.X = self.pos_max.X;
                if (self.pos.Y < self.pos_min.Y) self.pos.Y = self.pos_min.Y;
                if (self.pos.Y > self.pos_max.Y) self.pos.Y = self.pos_max.Y;

            }

            //public void activate_objs()
            //{
            //    for (int i = Game.game_world.cur_area.objs_queue.Count - 1; i >= 0; i--)
            //    {
            //        obj v = Game.game_world.cur_area.objs_queue[i];

            //        Rectangle area = spawn_rect();
            //        if (v.x <= (area.Right + v.w_half()) && v.x >= (area.Left - v.w_half()) && v.y <= (area.Bottom + v.h_half()) && v.y >= (area.Top - v.h_half()))
            //        //if (v.x < x)
            //        {
            //            // move to active list.
            //            v.activate();
            //        }
            //    }
            //}

            public override void _draw()
            {
                base._draw();
#if DEBUG
                if (inst.debug_draw_enabled)
                {
                    rect(pos_min.X, pos_min.Y, pos_max.X - 1, pos_max.Y - 1, 8);
                    rect(play_area_min.X, play_area_min.Y, play_area_max.X - 1, play_area_max.Y - 1, 9);
                }
#endif // DEBUG
            }

            public Vector2 cam_pos()
            {

                var self = this;
                //calculate camera shake.
                var shk = new Vector2(0, 0);
                if (self.shake_remaining > 0)
                {
                    shk.X = rnd(self.shake_force) - (self.shake_force / 2);
                    shk.Y = rnd(self.shake_force) - (self.shake_force / 2);

                }
                return new Vector2(self.pos.X - (inst.Res.X * 0.5f) + shk.X, self.pos.Y - (inst.Res.Y * 0.5f) + shk.Y);

            }

            public float pull_max_x()
            {
                return (pos.X + pull_threshold_offset.X) + pull_threshold;
            }

            public float pull_min_x()
            {
                return (pos.X + pull_threshold_offset.X) - pull_threshold;
            }

            public float pull_max_y()
            {
                return (pos.Y + pull_threshold_offset.Y) + pull_threshold;
            }

            public float pull_min_y()
            {
                return (pos.Y + pull_threshold_offset.Y) - pull_threshold;

            }

            public void shake(int ticks, float force)
            {
                shake_remaining = ticks;

                shake_force = force;
            }

            public bool is_obj_off_screen(sprite s)
            {
                if (s is player_controller)
                {
                    s = (s as player_controller).pawn;
                }
                return !inst.intersects_obj_box(s, pos.X, pos.Y, inst.Res.X * 0.5f, inst.Res.Y * 0.5f);
            }

            public bool is_pos_off_screen(float x, float y)
            {
                return !inst.intersects_point_box(x, y, pos.X, pos.Y, inst.Res.X * 0.5f, inst.Res.Y * 0.5f);
            }

            public bool is_pos_in_play_area(float x, float y)
            {
                Vector2 play_area_size = (play_area_max - play_area_min) * 0.5f;
                return inst.intersects_point_box(x, y, play_area_min.X + play_area_size.X, play_area_min.Y + play_area_size.Y, play_area_size.X, play_area_size.Y);
            }

            public bool is_obj_in_play_area(sprite s)
            {
                if (s is player_controller)
                {
                    s = (s as player_controller).pawn;
                }
                Vector2 play_area_size = (play_area_max - play_area_min) * 0.5f;
                return inst.intersects_obj_box(s, play_area_min.X + play_area_size.X, play_area_min.Y + play_area_size.Y, play_area_size.X, play_area_size.Y);
            }

            public bool is_obj_in_play_area(sprite s, Vector2 offset)
            {
                if (s is player_controller)
                {
                    s = (s as player_controller).pawn;
                }
                Vector2 play_area_size = (play_area_max - play_area_min) * 0.5f;
                return inst.intersects_obj_box(s, play_area_min.X + play_area_size.X + offset.X, play_area_min.Y + play_area_size.Y + offset.Y, play_area_size.X, play_area_size.Y);
            }
        }

        //math
        ////////////////////////////////

        bool intersects_obj_obj(sprite a, sprite b)
        {
            //return intersects_box_box(a.x,a.y,a.w,a.h,b.x,b.y,b.w,b.h)
            return intersects_box_box(
                a.cx, a.cy, a.cw * 0.5f, a.ch * 0.5f,
                b.cx, b.cy, b.cw * 0.5f, b.ch * 0.5f);
        }

        bool intersects_obj_box(sprite a, float x1, float y1, float w1, float h1)
        {
            return intersects_box_box(a.cx, a.cy, a.cw * 0.5f, a.ch * 0.5f, x1, y1, w1, h1);
        }

        bool intersects_point_obj(float px, float py, sprite b)
        {
            return intersects_point_box(px, py, b.cx, b.cy, b.cw * 0.5f, b.ch * 0.5f);
        }

        //point to box intersection.
        bool intersects_point_box(float px, float py, float x, float y, float w, float h)
        {
            if (flr(px) >= flr(x - (w)) && flr(px) < flr(x + (w)) && flr(py) >= flr(y - (h)) && flr(py) < flr(y + (h)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //box to box intersection
        bool intersects_box_box(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
        {
            var xd = x1 - x2;

            var xs = w1 + w2;
            if (abs(xd) >= xs)
            {
                return false;
            }

            var yd = y1 - y2;

            var ys = h1 + h2;

            if (abs(yd) >= ys)
            {
                return false;
            }

            return true;
        }


        //check if pushing into side tile and resolve.
        //requires self.dx,self.x,self.y, and 
        //assumes tile flag 0 == solid
        //assumes sprite size of 8x8
        //check if pushing into side tile and resolve.
        //requires self.dx,self.x,self.y, and 
        //assumes tile flag 0 == solid
        bool collide_side(sprite self, out Vector2 hit_point)
        {
            // Don't do collision of the player isn't moving sideways.
            //if (self.dx == 0)
            //{
            //    hit_point = Vector2.Zero;
            //    return false;
            //}


            //didn't hit anything solid.
            hit_point.X = 0;
            hit_point.Y = 0;

            //check for collision along inner-2-3rds
            //of sprite side.
            var offset_x = self.cw / 2.0f;
            var offset_y = self.ch / 3.0f;
            float correction_x = 0.0f;
            bool hit = false;
            //if (self.dx < 0)
            //{
            //    dir = -1.0f;
            //    offset_x *= -1.0f;
            //    correction_x = 8.0f;
            //}

            // Right Tiles
            for (float i = -offset_y; i <= offset_y; i += 2) // for i=-(self.w/3),(self.w/3),2 do
            {

                if (fget(mget_tiledata(flr((self.cx + (offset_x)) / 8), flr((self.cy + i) / 8)), 0))
                {
                    self.dx = 0;
                    self.x = (flr(((self.cx + (offset_x)) / 8)) * 8) + correction_x - (offset_x) - (self.cx_offset);
                    hit_point.X = self.cx + (offset_x);
                    hit_point.Y = self.cy + i;
                    //return true;
                    hit = true;
                }

            }

            // Left Tiles
            offset_x *= -1.0f;
            correction_x = 8.0f;
            for (float i = -offset_y; i <= offset_y; i += 2) // for i=-(self.w/3),(self.w/3),2 do
            {
                if (fget(mget_tiledata(flr((self.cx + (offset_x)) / 8), flr((self.cy + i) / 8)), 0))
                {
                    self.dx = 0;
                    self.x = (flr(((self.cx + (offset_x)) / 8)) * 8) + correction_x - (offset_x) - (self.cx_offset);
                    hit_point.X = self.cx + (offset_x) - 1;
                    hit_point.Y = self.cy + i;
                    //return true;
                    hit = true;
                }

            }

            // Not hitting a tile, so try dynamic objects.
            foreach (PicoXObj o in objs)
            {
                sprite v = o as sprite;
                if (v != null && v != self)
                {
                    if (v.is_platform)
                    {
                        // Left objects.

                        // check for collision minus the top 2 pixels and the bottom 2 pixels (hence -4)
                        //if (intersects_obj_box(self, v.x, v.y, v.cw * 0.5f, (v.ch - 4) * 0.5f))
                        if (intersects_box_box(self.cx - self.cw * 0.5f, self.cy, 0.5f, self.ch / 3.0f, v.cx, v.cy, v.cw * 0.5f, (v.ch - 4) * 0.5f))
                        {
                            // Call before dx is 0
                            v.on_collide_side(self);

                            self.dx = 0;
                            //self.x = (/*flr*/(v.x - (v.cw * dir) * 0.5f)) - ((self.cw * dir) * 0.5f);
                            // +1 is to fix a bug where the player seems to get sucked into the side of platforms
                            // when pushed.
                            self.x = (/*flr*/(v.cx + v.cw * 0.5f)) + (self.cw * 0.5f) - self.cx_offset + 1.0f;

                            // We don't really know the hit point, so just put it at the center on the edge that hit.
                            hit_point.X = self.cx + (offset_x);
                            hit_point.Y = self.cy;

                            //return true;
                            hit = true;
                        }

                        // Right objects.

                        if (intersects_box_box(self.cx + self.cw * 0.5f, self.cy, 0.5f, self.ch / 3.0f, v.cx, v.cy, v.cw * 0.5f, (v.ch - 4) * 0.5f))
                        {
                            // Call before dx is 0
                            v.on_collide_side(self);

                            self.dx = 0;
                            self.x = (/*flr*/(v.cx - v.cw * 0.5f)) - (self.cw * 0.5f) - self.cx_offset - 1.0f;

                            // We don't really know the hit point, so just put it at the center on the edge that hit.
                            hit_point.X = self.cx - (offset_x);
                            hit_point.Y = self.cy;

                            //return true;
                            hit = true;
                        }
                    }
                }
            }

            //return false;
            return hit;
        }


        //check if pushing into floor tile and resolve.
        //requires self.dx,self.x,self.y,self.grounded,self.airtime and 
        //assumes tile flag 0 or 1 == solid
        bool collide_floor(sprite self)
        {
            Vector2 temp;
            return collide_floor(self, out temp);
        }
        bool collide_floor(sprite self, out Vector2 hit_point)
        {
            //didn't hit anything solid.
            hit_point.X = 0;
            hit_point.Y = 0;

            //only check for ground when falling.
            if (self.dy < 0)
            {
                return false;
            }

            //check for collision at multiple points along the bottom
            //of the sprite: left, center, and right.
            var offset_x = self.cw / 3.0f; // only check inner 2-3rds
            var offset_y = self.ch / 2.0f;

            float? new_y = null;

            byte collision_flag = 0;

            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                var box_x = self.cx;
                var box_y = self.cy;
                var box_w_half = self.cw * 0.5f;
                var box_h_half = self.ch * 0.5f;


                var y = flr((box_y + box_h_half) / 8);
                var y_actual = flr((self.y + box_h_half) / 8);
                byte tile_flag = fget(mget_tiledata(flr((box_x + i) / 8), y));
                if ((tile_flag & 1) << 0 != 0 || is_packed_tile(tile_flag, packed_tile_types.pass_through))
                {
                    new_y = (flr(y) * 8) - box_h_half + (self.y - self.cy);
                    collision_flag = tile_flag;

                    hit_point.X = self.cx + i;
                    hit_point.Y = self.cy + offset_y;

                    break;
                }
            }

            // If we didn't hit a tile, try dynamic objects.
            if (!new_y.HasValue)
            {
                foreach (PicoXObj o in objs)
                {
                    sprite v = o as sprite;
                    if (v != null && v != self)
                    {
                        // TODO: Should all objects calling this function be checking against other solid objects?
                        //       Does it still make sense that this is player only?
                        // TODO: Badguy has a "solid" flag which is uses to see if it should call the collision
                        //       functions at all. Perhaps that should be moved to sprite, and checked here, instead
                        //       of is_platform?
                        if ((self == pc.pawn || self.is_platform) && v.is_platform)
                        {
                            // Check a 1 pixel high box along the bottom the the player.
                            // Adding 2 to the solid because that is what solids do in their update to stick to
                            // objects when moving away from them.
                            if (inst.intersects_box_box(self.cx, self.cy + self.ch * 0.5f, self.cw * 0.5f, 1, v.cx, v.cy, v.cw * 0.5f, (v.ch + 2) * 0.5f))
                            {
                                new_y = (/*flr*/(v.cy - v.ch * 0.5f)) - (self.ch * 0.5f) - (self.cy_offset);
                                // fake standard collision.
                                collision_flag = 1 << 0;

                                // We don't really know the hit point, so just put it at the center on the edge that hit.
                                hit_point.X = self.cx;
                                hit_point.Y = self.cy + offset_y;

                                break;
                            }
                        }
                    }
                }
            }

            if (new_y.HasValue)
            {
                self.dy = 0;
                self.y = new_y.Value;
                self.grounded = collision_flag;
                self.airtime = 0;
                return true;
            }

            if (self.stay_on && !self.launched)
            {
                self.dx *= -1;
                self.x += self.dx;
                // fake collision flag
                collision_flag = 1 << 0;
            }

            return false;
        }

        //check if pushing into roof tile and resolve.
        //requires self.dy,self.x,self.y, and 
        //assumes tile flag 0 == solid
        bool collide_roof(sprite self)
        {
            if (self.dy >= 0)
            {
                return false;
            }

            //check for collision at multiple points along the top
            //of the sprite: left, center, and right.
            var offset_x = self.cw / 3.0f; // check the inner 2 3rds
            var offset_y = self.ch / 2.0f;

            bool hit_roof = false;

            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                if (fget(mget_tiledata(flr((self.cx + i) / 8), flr((self.cy - (offset_y)) / 8)), 0))
                {
                    self.dy = 0;
                    self.y = flr((self.cy - (offset_y)) / 8) * 8 + 8 + (offset_y) - self.cy_offset;
                    self.jump_hold_time = 0;
                    hit_roof = true;
                    break;
                }
            }

            if (!hit_roof)
            {
                foreach (PicoXObj o in objs)
                {
                    sprite v = o as sprite;
                    if (v != null && v != self)
                    {
                        if ((self == pc.pawn || self.is_platform) && v.is_platform)
                        {
                            // Check a 1 pixel box along the bottom of the player.
                            // Using 0.5f because that seems more correct but im not totally sure.
                            if (inst.intersects_box_box(self.cx, self.cy - self.ch * 0.5f, self.cw * 0.5f, 0.5f, v.cx, v.cy, v.cw * 0.5f, (v.ch) * 0.5f))
                            {
                                // Take the dy of the player or the solid, which ever is more downward.
                                // This ensure that the player doesn't kind of "float" along the bottom of the
                                // solid. We also min it to 0 so that if both are moving upwards, the player is
                                // at least stopped.
                                self.dy = min(0, max(v.dy, self.dy));
                                self.y = (/*flr*/(v.cy + v.ch * 0.5f)) + (self.ch * 0.5f) - self.cy_offset;
                                self.jump_hold_time = 0;
                                hit_roof = true;

                                break;
                            }
                        }
                    }
                }
            }

            return hit_roof;
        }

        public class hit_pause_manager : PicoXObj
        {
            public enum pause_reason
            {
                bounce,
                death,
                artifact_picked_up,
                gem_picked_up,

                message_box_open,
                level_trans,
            }

            Dictionary<pause_reason, int> pause_times = new Dictionary<pause_reason, int>()
            {
                { pause_reason.bounce, 0 }, // no pause for now. happens too much.
                { pause_reason.death, 30 },
                { pause_reason.artifact_picked_up, 30 },
                { pause_reason.gem_picked_up, 0 },
                { pause_reason.message_box_open, 1 },
                { pause_reason.level_trans, 1 },
            };

            public int pause_time_remaining { get; protected set; }

            public hit_pause_manager()
            {
                pause_time_remaining = 0;
            }

            public void start_pause(pause_reason reason)
            {
                pause_time_remaining = (int)inst.max(pause_time_remaining, pause_times[reason]);
            }

            public override void _update60()
            {
                base._update60();

                pause_time_remaining = (int)inst.max(0, pause_time_remaining - 1);
            }

            public bool is_paused()
            {
                return pause_time_remaining > 0;
            }
        }

        public class block_restorer : PicoXObj
        {
            public int map_x;
            public int map_y;
            public int time_remaining;
            packed_tile_types type;

            public block_restorer(int map_x, int map_y, int life_span, packed_tile_types type) : base()
            {
                this.map_x = map_x;
                this.map_y = map_y;
                time_remaining = life_span;
                this.type = type;
            }

            public override void _update60()
            {
                base._update60();

                time_remaining -= 1;

                if (time_remaining <= 0)
                {
                    switch(type)
                    {
                        case packed_tile_types.vanishing:
                            {
                                inst.change_meta_tile(map_x, map_y, new int[] { 834, 835, 850, 851 }, 0);
                                break;
                            }

                        case packed_tile_types.dissolving:
                            {
                                inst.change_meta_tile(map_x, map_y, new int[] { 40, 41, 56, 57 }, 1);
                                break;
                            }
                    }
                    inst.objs_remove_queue.Add(this);
                }
            }
        }

        public class block_exploder : PicoXObj
        {
            public int map_x;
            public int map_y;
            public int time_remaining;

            public block_exploder(int map_x, int map_y, int life_span) : base()
            {
                this.map_x = map_x;
                this.map_y = map_y;
                time_remaining = life_span;
            }

            public override void _update60()
            {
                base._update60();

                time_remaining -= 1;

                if (time_remaining <= 0)
                {

                    for (int y = -2; y <= 2; y+=2)
                    {
                        for (int x = -2; x <= 2; x+=2)
                        {
                            // Only do up down left and right.
                            if (abs(x) == abs(y))
                            {
                                continue;
                            }
                            int mx = map_x + x;
                            int my = map_y + y;
                            if (inst.is_packed_tile(fget(mget_tiledata(mx, my)), packed_tile_types.rock_smash))
                            {
                                inst.smash_rock(mx, my, true);
                            }
                        }
                    }
                    
                    inst.objs_remove_queue.Add(this);
                }
            }
        }

        private void smash_rock(int mx, int my, bool with_explode)
        {
            var grid_pos = inst.map_pos_to_meta_tile(mx, my);

            for (int i = 0; i <= 1; i++)
            {
                for (int j = 0; j <= 1; j++)
                {
                    var final_x = (grid_pos.X + i) * 8 + 4;
                    var final_y = (grid_pos.Y + j) * 8 + 4;
                    inst.objs_add_queue.Add(
                        new simple_fx_particle(-1 + (i * 2), (1 - j + 1) * -3, mget(final_x / 8, final_y / 8))
                        {
                            x = final_x,
                            y = final_y,
                            bank = 1,
                        });
                }
            }

            bool water = false;
            // Don't check below because water doesn't rise.
            for (int y = -2; y <= 0; y += 2)
            {
                for (int x = -2; x <= 2; x += 2)
                {
                    // Only do up down left and right.
                    if (abs(x) == abs(y))
                    {
                        continue;
                    }
                    int fmx = grid_pos.X + x;
                    int fmy = grid_pos.Y + y;
                    if (inst.is_packed_tile(fget(mget_tiledata(fmx, fmy)), packed_tile_types.water))
                    {
                        water = true;
                    }
                }
            }

            if (!water)
            {
                inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 }, 0);
            }
            else
            {
                inst.change_meta_tile(mx, my, new int[] { 50, 51, 50, 51 }, 1);
            }

            if (with_explode)
            {
                inst.objs_add_queue.Add(new block_exploder(mx, my, 2));
            }
        }

        public class map_link : sprite
        {
            public enum transition_dir
            {
                vert,
                horz,
                none,
            };

            public string dest_map_path;
            public string dest_spawn_name;
            public transition_dir trans_dir;
            public bool require_up_press = false;

            public map_link()
            {
                trans_dir = transition_dir.none;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.pc.pawn.supports_map_links && inst.intersects_obj_obj(this, inst.pc.pawn))
                {
                    if (!require_up_press || btnp(2))
                    {
                        inst.active_map_link = this;
                        inst.queued_map = dest_map_path;
                        // Store the offset relative to the map link, so that on the other side of the transition,
                        // we can offset the same amount.
                        //inst.spawn_offset.X = inst.pc.pawn.x - inst.game_cam.cam_pos().X;
                        //inst.spawn_offset.Y = inst.pc.pawn.y - inst.game_cam.cam_pos().Y;
                    }
                }
            }

            public override void _draw()
            {
                // do nothing.
                base._draw();
            }
        }

        public class gem_pickup : sprite
        {
            // Index within the level is resides in.
            public int id;

            public gem_pickup(int id)
            {
                anims = new Dictionary<string, anim>()
                    {
                        {
                            "default",
                            new anim()
                            {
                                ticks=15,//how long is each frame shown.
                                frames = new int[][]
                                {
                                    create_anim_frame(292, 2, 2),
                                    create_anim_frame(262, 2, 2),
                                    create_anim_frame(264, 2, 2),
                                    create_anim_frame(266, 2, 2),
                                }
                            }
                        },
                    };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                this.id = id;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(inst.pc.pawn, this))
                {
                    printh("found gem (before): " + Convert.ToString(inst.pc.found_gems_00, 2) + ", " + Convert.ToString(inst.pc.found_gems_01, 2));

                    // Store prior to modding value.
                    int cached_id = id;

                    Action<UInt32> set_mask = delegate (UInt32 mask)
                    {
                        if (cached_id <= 31)
                        {
                            inst.pc.found_gems_00 |= mask;
                        }
                        else
                        {
                            inst.pc.found_gems_01 |= mask;
                        }
                    };

                    // value from 0-31
                    id %= 32;

                    UInt32 gem_mask = (UInt32)1 << id;// + (inst.gems_per_level * inst.cur_level_id);

                    set_mask(gem_mask);

                    printh("found gem (mask): " + Convert.ToString(gem_mask, 2));
                    printh("found gem (after): " + Convert.ToString(inst.pc.found_gems_00, 2) + ", " + Convert.ToString(inst.pc.found_gems_01, 2));

                    //                    dset((uint)Game1.cartdata_index.gems, (int)inst.pc.found_gems);

                    inst.objs_remove_queue.Add(this);
                    inst.hit_pause.start_pause(hit_pause_manager.pause_reason.gem_picked_up);
                    return;
                }
            }

            public override void _draw()
            {
                base._draw();

                //print(id.ToString(), x, y, 8);
            }
        }

        public class artifact_pickup : sprite
        {
            artifacts id;

            public artifact_pickup(artifacts id)
            {
                this.id = id;

                if (id >= artifacts.health_start && id <= artifacts.health_end)
                {
                    anims = new Dictionary<string, anim>()
                            {
                                {
                                    "default",
                                    new anim()
                                    {
                                        ticks=10,//how long is each frame shown.
                                        frames = new int[][]
                                        {
                                            create_anim_frame(300, 2, 2),
                                            create_anim_frame(300, 2, 2),
                                            create_anim_frame(300, 2, 2),
                                            create_anim_frame(302, 2, 2),
                                        }
                                    }
                                },
                            };
                }
                else
                {
                    anims = new Dictionary<string, anim>()
                            {
                                {
                                    "default",
                                    new anim()
                                    {
                                        ticks=30,//how long is each frame shown.
                                        frames = new int[][]
                                        {
                                            create_anim_frame(268, 2, 2),
                                            create_anim_frame(270, 2, 2),
                                        }
                                    }
                                },
                            };
                }

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(inst.pc.pawn, this))
                {
                    inst.pc.found_artifacts |= id;

                    //dset((uint)Game1.cartdata_index.artifacts, (int)inst.pc.found_artifacts);

                    if (id >= artifacts.health_start && id <= artifacts.health_end)
                    {
                        inst.pc.pawn.hp = inst.pc.pawn.get_hp_max();
                    }
                    inst.objs_remove_queue.Add(this);
                    inst.hit_pause.start_pause(hit_pause_manager.pause_reason.artifact_picked_up);

                    string display_name = "";

                    switch (id)
                    {
                        case artifacts.dash_pack:
                            {
                                display_name = "dash pack";
                                break;
                            }
                        case artifacts.jump_boots:
                            {
                                display_name = "jump boots";
                                break;
                            }

                        case artifacts.rock_smasher:
                            {
                                display_name = "rock smasher";
                                break;
                            }

                        case artifacts.ground_slam:
                            {
                                display_name = "ground slam";
                                break;
                            }

                        case artifacts.light:
                            {
                                display_name = "flash light";
                                break;
                            }

                        case artifacts.air_tank:
                            {
                                display_name = "air tank";
                                break;
                            }

                        default:
                            {
                                display_name = "heart container";
                                break;
                            }
                    }
                    inst.message = new message_box();
                    inst.message.set_message("the title", display_name + " acquired!");

                    return;
                }
            }
        }

        public class rocket_ship : player_top
        {
            player_top old_pawn;
            int ticks_possesed = 0;
            int ticks_unpossed = 999; // start "fully possessed"
            int gems_required_to_fly = 8;
            int gems_required_to_win = 13;
            bool landing_queued = false;

            public rocket_ship()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "walk_down",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(650, 4, 4),
                            }
                        }
                    },
                    {
                        "walk_left",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(714, 4, 4),
                            }
                        }
                    },
                    {
                        "walk_up",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(296, 4, 4),
                            }
                        }
                    },
                    {
                        "walk_right",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(586, 4, 4),
                            }
                        }
                    },

                    {
                        "idle_down",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(650, 4, 4),
                            }
                        }
                    },
                    {
                        "idle_left",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(714, 4, 4),
                            }
                        }
                    },
                    {
                        "idle_up",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(296, 4, 4),
                            }
                        }
                    },
                    {
                        "idle_right",
                        new anim()
                        {
                            loop = true,
                            ticks=1,
                            frames = new int[][]
                            {
                                create_anim_frame(586, 4, 4),
                            }
                        }
                    },
                };

                set_anim("idle_down");

                w = 32;
                h = 32;
                cw = 16;
                ch = 16;

                dx = 0;
                dy = 0;

                supports_map_links = false;
                is_flying = true;
            }

            public override bool on_reached_destination()
            {
                if (!base.on_reached_destination())
                {
                    if (landing_queued)
                    {
                        if(mfget(flr(x / 8), flr(y / 8), 0, 1))
                        {
                            landing_queued = false;
                            return false;
                        }

                        foreach(PicoXObj o in inst.objs)
                        {
                            map_link link = o as map_link;
                            if (link != null)
                            {
                                if (inst.intersects_obj_obj(this, link))
                                {
                                    landing_queued = false;
                                    return false;
                                }
                            }
                        }

                        old_pawn.x = old_pawn.dest_x = x;
                        old_pawn.y = old_pawn.dest_y = y;
                        inst.pc.possess(old_pawn);

                        Int32 packed_pos = flr(dest_x / 8.0f) | (flr(dest_y / 8.0f) << 16);
                        dset((int)cartdata_index.ship_map_pos_packed, packed_pos);

                        // Save the player pos too.
                        packed_pos = flr(inst.pc.pawn.x / 8.0f) | (flr(inst.pc.pawn.y / 8.0f) << 16);
                        dset((int)cartdata_index.overworld_pos_packed, packed_pos);

                        old_pawn = null;
                        ticks_unpossed = 0;

                        landing_queued = false;

                        // handled.
                        return true;
                    }
                }

                // unhandled.
                return false;
            }

            public override void _update60()
            {
                base._update60();

                if (this == inst.pc.pawn)
                {
                    if (btnp(4) && ticks_possesed > 1)
                    {
                        // queue up the landing but wait to reach destination.
                        landing_queued = true;
                    }

                    ticks_possesed++;
                }
                // Make sure we didn't just exit the ship this frame (in base update).
                else if (ticks_unpossed > 0 && inst.intersects_obj_obj(this, inst.pc.pawn))
                {
                    int gem_count = inst.pc.get_gem_count();
                    if (btnp(4) && inst.message == null)
                    {

                        //ui_box bg = new ui_box() { x = inst.Res.X * 0.5f - 32, y = inst.Res.Y * 0.5f - 32, width = 64, height = 64, color = 0, fill = true };
                        //ui_menu_scene_list rocket_menu = new ui_menu_scene_list() { x = inst.Res.X * 0.5f, y = inst.Res.Y * 0.5f };
                        ////ui_widget container = new ui_widget() { x = inst.Res.X * 0.5f, y = inst.Res.Y * 0.5f };
                        //ui_menu_scene_list_item fly_ship_item = new ui_menu_scene_list_item().add_child(new ui_text() { display_string = "fly", color = 7 }) as ui_menu_scene_list_item;
                        //ui_menu_scene_list_item leave_planet_item = new ui_menu_scene_list_item() { y = 6 }.add_child(new ui_text() { display_string = "win", color = 7 }) as ui_menu_scene_list_item;

                        //rocket_menu.add_child(fly_ship_item).add_child(leave_planet_item);

                        //inst.ui_scene.add_child(bg).add_child(rocket_menu);

                        if (gem_count >= gems_required_to_fly)
                        {
                            old_pawn = inst.pc.pawn as player_top;
                            inst.pc.possess(this);
                            ticks_possesed = 0;
                        }
                        else
                        {
                            inst.message = new message_box();
                            inst.message.set_message("title", gem_count.ToString() + "/" + gems_required_to_fly.ToString() + " gems required to fly...");
                        }
                    }
                    else if (btnp(5) && inst.message == null)
                    {
                        if (gem_count >= gems_required_to_win)
                        {
                            inst.message = new message_box();
                            inst.message.set_message("title", "ship powers up, and lift off!", () => { inst.set_game_state(game_state.game_win); });
                        }
                        else
                        {
                            inst.message = new message_box();
                            inst.message.set_message("title", gem_count.ToString() + "/" + gems_required_to_win.ToString() + " gems required to leave planet...");
                        }
                    }
                }

                // Not in the ship.
                if (this != inst.pc.pawn)
                {
                    ticks_unpossed++;
                }
            }

            public override void _draw()
            {
                // TODO: use sprfxset for dynamic shadow effect.
                inst.apply_pal(inst.shadow_pal);
                base._draw();
                pal();
                float temp_y = y;
                const float min_h = 2.0f;
                const float max_h = 6.0f;
                const float intro_ticks = 120;
                const float outro_ticks = 30;
                if (old_pawn != null)
                {

                    if (ticks_possesed < intro_ticks)
                    {
                        y -= flr((ticks_possesed / intro_ticks) * max_h);
                    }
                    else
                    {
                        y -= ((cos((ticks_possesed - intro_ticks) * 0.005f) + 1.0f) * ((max_h - min_h) *0.5f)) + min_h;
                    }
                }
                else
                {
                    if (ticks_unpossed < outro_ticks)
                    {
                        y -= flr((1.0f - (ticks_unpossed / outro_ticks)) * max_h);
                    }
                }
                base._draw();
                y = temp_y;
            }
        }

        // TODO:
        // * Trigger save when entering checkpoint.
        // * Show as active when returning to the map with the last active checkpoint.
        // * Save the last used checkpoint, and load into that map when resuming game.
        public class checkpoint : sprite
        {
            // Is this the currently active checkpoint.
            public bool is_activated { get; private set; }

            // The name of the map where this checkpoint spawned.
            public string map_name;

            public bool touching;

            public checkpoint()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "off",
                        new anim()
                        {
                            ticks=30,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(524, 4, 4),
                            }
                        }
                    },
                    {
                        "on",
                        new anim()
                        {
                            ticks=10,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(512, 4, 4),
                                create_anim_frame(516, 4, 4),
                                create_anim_frame(520, 4, 4),
                            }
                        }
                    },
                };

                is_activated = false;

                set_anim("off");

                w = 32;
                h = 32;
                cw = 16;
                ch = 16;

                touching = false;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(this, inst.pc.pawn))
                {
                    if (!touching)
                    {
                        dset((uint)Game1.cartdata_index.gems_00, (int)inst.pc.found_gems_00);
                        dset((uint)Game1.cartdata_index.gems_01, (int)inst.pc.found_gems_01);
                        dset((uint)Game1.cartdata_index.artifacts, (int)inst.pc.found_artifacts);

                        if (!is_activated)
                        {
                            // Deactive the currently active one.
                            if (inst.last_activated_checkpoint != null)
                            {
                                inst.last_activated_checkpoint.deactivate();
                            }

                            inst.last_activated_checkpoint = this;
                            map_name = inst.current_map;
                            is_activated = true;
                            set_anim("on");

                            // Check if any other checkpoints are active on the map, and
                            // deactivate them.
                            //foreach (PicoXObj o in inst.objs)
                            //{
                            //    checkpoint c = o as checkpoint;
                            //    if (c != null && c != this)
                            //    {
                            //        c.deactivate();
                            //    }
                            //}
                        }
                    }
                    touching = true;
                }
                else
                {
                    touching = false;
                }
            }

            public void deactivate()
            {
                is_activated = false;
                set_anim("off");
            }
        }

        public enum game_state
        {
            main_menu,
            level_trans_exit,
            level_trans_enter,
            gameplay,
            gameplay_dead,
            game_over,
            game_win,
        }

        player_controller pc;
        cam game_cam;
        cam_manager game_cam_man;

        List<PicoXObj> objs;
        List<PicoXObj> objs_remove_queue;
        List<PicoXObj> objs_add_queue;

        game_state cur_game_state;
        game_state prev_game_state;
        uint time_in_state;
        // TODO: why am I not just using the BufferedKey?
        complex_button start_game;
        int cur_map_bank = 0;

        public hit_pause_manager hit_pause;

        public string current_map = "Content/raw/map_ow_top.tmx";
        public string queued_map = "Content/raw/map_ow_top.tmx";
        public map_link active_map_link;

        public int cur_level_id = 0;
        public int gems_per_level = 2;

        public checkpoint last_activated_checkpoint;

        int level_trans_time = 10;

        bool debug_draw_enabled = false;

        public class message_box
        {
            public int chars_per_line;

            public string title { get; private set; }

            public Action on_close_delegate;

            public bool open_this_frame = true;

            public int longest_line;

            public void set_message(string title, string body, Action on_close = null)
            {
                body_with_breaks = new List<string>();

                chars_per_line = 32;

                string[] words = body.Split(' ');
                string line = "";

                for (int i = 0; i < words.Length; i++)
                {
                    // TODO: if a single word is longer than the single line, then add hyphen and move to new line.
                    if (words[i].Length + line.Length < chars_per_line)
                    {
                        line += words[i] + " ";
                        longest_line = (int)inst.max(longest_line, line.Length);
                    }
                    else
                    {
                        body_with_breaks.Add(line);
                        line = words[i] + " ";
                    }
                }

                // Add the last line.
                // TODO: Handle case where the last word triggered an Add already.
                body_with_breaks.Add(line);

                on_close_delegate = on_close;
            }

            public List<string> body_with_breaks { get; private set; }
        };

        public message_box message;

        // save game index info.
        public enum cartdata_index : uint
        {
            version = 0,
            gems_00 = 1,
            artifacts = 2,
            ship_map_pos_packed = 3,
            overworld_pos_packed = 4,
            gems_01 = 5,
        }

        public Game1() : base()
        {
            // MUST BE DONE BEFORE ANY PICOXOBJ ARE CREATED
            inst = this;
        }

        private bool ParseTmxObjectToBadGuy(TmxObject o)
        {
            badguy b = null;

            if (string.Compare(o.Type, "spawn_chopper", true) == 0)
            {
                b = new chopper(Int32.Parse(o.Properties["duration"]), Int32.Parse(o.Properties["dist"]))
                {
                    x = (float)o.X + ((float)o.Width * 0.5f),
                    y = (float)o.Y + ((float)o.Height * 0.5f),
                };
            }
            else if (string.Compare(o.Type, "spawn_lava_blaster", true) == 0)
            {
                b = new lava_blaster(Int32.Parse(o.Properties["dir"]))
                {
                    x = (float)o.X + ((float)o.Width * 0.5f),
                    y = (float)o.Y + ((float)o.Height * 0.5f),
                };
            }
            else if (string.Compare(o.Type, "spawn_rolley", true) == 0)
            {
                b = new badguy(Int32.Parse(o.Properties["dir"]))
                {
                    x = (float)o.X + ((float)o.Width * 0.5f),
                    y = (float)o.Y + ((float)o.Height * 0.5f),
                };
            }

            if (b != null)
            {
                string respawn_string;
                if (o.Properties.TryGetValue("respawn", out respawn_string))
                {
                    if (bool.Parse(respawn_string) == true)
                    {
                        b.respawn_data = o;
                    }
                }
                string respawn_delay_string;
                if (o.Properties.TryGetValue("respawn_delay", out respawn_delay_string))
                {
                    b.respawn_delay = int.Parse(respawn_delay_string);
                }
                b.x_initial = b.x;
                b.y_initial = b.y;
                objs_add_queue.Add(b);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void initialize_map(ref Vector2 spawn_point, bool debug_hot_reload)
        {
            current_map = queued_map;

            Vector2 cam_area_min = Vector2.Zero;
            Vector2 cam_area_max = Vector2.Zero;
            float pull_threshold_offset_y = 0;

            objs.Clear();
            objs_remove_queue.Clear();
            objs_add_queue.Clear();

            cur_map_config = new map_config();
            game_cam_man = new cam_manager();

            reloadmap(GetMapString());

            TmxMap TmxMapData = new TmxMap(GetMapString());

            cur_map_config.map_size_pixels.X = TmxMapData.Width * 8;
            cur_map_config.map_size_pixels.Y = TmxMapData.Height * 8;

            // Figure out what bank this map uses.
            // NOTE: For now we assume that each map uses only 1 bank.
            for (int i = 0; i < GetSheetPath().Count; i++)
            {
                string file_name = Path.GetFileNameWithoutExtension((TmxMapData.Tilesets[0]).Image.Source);
                if (GetSheetPath()[i].EndsWith(file_name))
                {
                    cur_map_bank = i;
                    break;
                }
            }

            player_pawn pawn = null;

            int gem_id = 0;

            if (TmxMapData.Properties.ContainsKey("level_id"))
            {
                cur_level_id = int.Parse(TmxMapData.Properties["level_id"]);

                System.Diagnostics.Debug.Assert(cur_level_id < (32 / gems_per_level), "Using level id that won't fit in 32 bits.");
            }
            else
            {
                cur_level_id = -1;
            }

            if (TmxMapData.Properties.ContainsKey("darkness_level"))
            {
                cur_map_config.darkness_level = int.Parse(TmxMapData.Properties["darkness_level"]);
            }

            if (TmxMapData.Properties.ContainsKey("is_tower"))
            {
                cur_map_config.is_tower = bool.Parse(TmxMapData.Properties["is_tower"]);
            }

            foreach (var group in TmxMapData.ObjectGroups)
            {
                foreach (var o in group.Objects)
                {
                    if (string.Compare(o.Type, "spawn_point", true) == 0 && !debug_hot_reload)
                    {
                        if (!string.IsNullOrEmpty(active_map_link?.dest_spawn_name))
                        {
                            if (active_map_link.dest_spawn_name != o.Name)
                            {
                                continue;
                            }
                        }
                        // Account for the case of a checkpoint.
                        if (spawn_point == Vector2.Zero)
                        {
                            spawn_point = new Vector2((float)o.X + ((float)o.Width * 0.5f), (float)o.Y + ((float)o.Height * 0.5f));
                        }

                        // mandatory field.
                        switch (o.Properties["type"])
                        {
                            case "top":
                                {
                                    pawn = new player_top()
                                    {
                                        x = spawn_point.X,
                                        y = spawn_point.Y,
                                        w = 16,
                                        h = 16,
                                        cw = 16,
                                        ch = 16,
                                    };
                                    (pawn as player_top).dest_x = pawn.x;
                                    (pawn as player_top).dest_y = pawn.y;

                                    // Save the overworld position every time we enter the overworld.
                                    // This is only ever applied when coming from the main menu, with the
                                    // assumption that the player cannot die in the open world.
                                    Int32 packed_pos = flr(pawn.x / 8.0f) | (flr(pawn.y / 8.0f) << 16);
                                    dset((int)cartdata_index.overworld_pos_packed, packed_pos);

                                    break;
                                }

                            case "side":
                                {
                                    pawn = new player_side()
                                    {
                                        x = spawn_point.X,
                                        y = spawn_point.Y,
                                    };
                                    break;
                                }
                        }
                    }
                    else if (string.Compare(o.Type, "cam_area", true) == 0)
                    {
                        cam_area_min = new Vector2((float)o.X, (float)o.Y);
                        cam_area_max = new Vector2((float)o.X + (float)o.Width, (float)o.Y + (float)o.Height);
                        string pull_threshold_offset_y_string;
                        if (o.Properties.TryGetValue("pull_threshold_offset_y", out pull_threshold_offset_y_string))
                        {
                            pull_threshold_offset_y = float.Parse(pull_threshold_offset_y_string);
                        }
                        else
                        {
                            pull_threshold_offset_y = 0.0f;
                        }

                        const int hud_height = 16;

                        // Account for the fact that the camera area can be smaller than the game resolution.
                        // This could be fixed in content by always setting a min cam size of ResX/Y, but this
                        // allows us to change the resolution without having to update content.
                        Vector2 cam_area_min_og = cam_area_min;
                        Vector2 cam_area_max_og = cam_area_max;
                        Vector2 cam_area = cam_area_max - cam_area_min;
                        Vector2 cam_delta_half = (Res - cam_area) * 0.5f;

                        // Is the camera area smaller than the resolution? If so, adjust it (while keeping
                        // it centered around the same point) to match the resolution.
                        // NOTE: At time of writing we still render the map outside the camera area.
                        if (cam_area.X < Res.X)
                        {
                            cam_area_min.X -= cam_delta_half.X;
                            cam_area_max.X += cam_delta_half.X;
                        }
                        if (cam_area.Y < Res.Y)
                        {
                            cam_area_min.Y -= cam_delta_half.Y - 8;
                            cam_area_max.Y += cam_delta_half.Y - 8;
                        }

                        cam new_cam = new cam(pc)
                        {
                            pos_min = cam_area_min + new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f - hud_height),
                            pos_max = cam_area_max - new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f),
                            play_area_min = cam_area_min_og,
                            play_area_max = cam_area_max_og,
                            pull_threshold_offset = new Vector2(0, pull_threshold_offset_y),
                        };
                        if (cur_map_config.is_tower)
                        {
                            new_cam.pull_threshold = 0;
                        }
                        //game_cam.jump_to_target();
                        game_cam_man.cameras.Add(new_cam);
                    }
                    else if (ParseTmxObjectToBadGuy(o))
                    {
                        // noop
                    }
                    else if (string.Compare(o.Type, "spawn_steam_spawner", true) == 0)
                    {
                        objs_add_queue.Add(
                                new steam_spawner()
                                {
                                    x = (float)o.X + ((float)o.Width * 0.5f),
                                    y = (float)o.Y + ((float)o.Height * 0.5f),
                                }
                            );
                    }
                    else if (string.Compare(o.Type, "spawn_dart_gun", true) == 0)
                    {
                        string start_delay_string = "0";
                        int start_delay = 0;
                        if (o.Properties.TryGetValue("start_delay", out start_delay_string))
                        {
                            start_delay = int.Parse(start_delay_string);
                        }

                        // A simplified way to have a somewhat synchronized start delay across multiple objects.
                        // Each object should have the same start_delay property, but then the start_delay_index
                        // can be incremented across the objects.
                        string start_delay_index_string = "0";
                        if (o.Properties.TryGetValue("start_delay_index", out start_delay_index_string))
                        {
                            start_delay *= int.Parse(start_delay_index_string);
                        }

                        dart_gun d = new dart_gun(float.Parse(o.Properties["dir_x"]), float.Parse(o.Properties["dir_y"]))
                        {
                            x = (float)o.X + ((float)o.Width * 0.5f),
                            y = (float)o.Y + ((float)o.Height * 0.5f),
                            start_delay = start_delay,
                        };

                        // Only set these if the property exists, so that it can fallback to class defaults.
                        string firing_delay_string = "0";
                        if (o.Properties.TryGetValue("firing_delay", out firing_delay_string))
                        {
                            d.firing_delay = int.Parse(firing_delay_string);
                        }
                        
                        objs_add_queue.Add(d);
                    }
                    else if (string.Compare(o.Type, "spawn_geyser", true) == 0)
                    {
                        geyser g = new geyser((float)o.X + (float)o.Width * 0.5f, (float)o.Y + ((float)o.Height * 0.5f), (int)o.Width, (int)o.Height, float.Parse(o.Properties["dir_x"]), float.Parse(o.Properties["dir_y"]));

                        objs_add_queue.Add(g);
                    }
                    else if (string.Compare(o.Type, "spawn_repeating_tile", true) == 0)
                    {
                        repeating_sprite r = new repeating_sprite((float)o.X + ((float)o.Width * 0.5f), (float)o.Y + ((float)o.Height * 0.5f), (int)(o.Width / 8.0f), (int)(o.Height / 8.0f)
                            , int.Parse(o.Properties["sprite_id"])
                            , int.Parse(o.Properties["bank"])
                            , int.Parse(o.Properties["num_frames"])
                            , int.Parse(o.Properties["ticks_per_frame"]));

                        objs_add_queue.Add(r);
                    }
                    else if (string.Compare(o.Type, "spawn_rocket_ship", true) == 0)
                    {
                        rocket_ship r = new rocket_ship()
                        {
                            x = (float)o.X + ((float)o.Width * 0.5f),
                            y = (float)o.Y + ((float)o.Height * 0.5f),
                        };

                        // right 16 bits = x
                        // left 16 bits = y
                        int pack_pos = dget((int)cartdata_index.ship_map_pos_packed);

                        // TODO: Come up with a better invalid value.
                        if (pack_pos != 0)
                        {
                            // Isolate the x and y components of the 32 bit value.
                            short unpacked_x = (short)(pack_pos & short.MaxValue);
                            short unpacked_y = (short)(pack_pos >> 16);

                            r.x = unpacked_x * 8.0f;
                            r.y = unpacked_y * 8.0f;
                        }

                        r.dest_x = r.x;
                        r.dest_y = r.y;

                        objs_add_queue.Add(r);
                    }
                    else if (string.Compare(o.Type, "spawn_checkpoint", true) == 0)
                    {
                        objs_add_queue.Add(
                                new checkpoint()
                                {
                                    x = (float)o.X + ((float)o.Width * 0.5f),
                                    y = (float)o.Y + ((float)o.Height * 0.5f),
                                }
                            );
                    }
                    else if (string.Compare(o.Type, "spawn_platform", true) == 0)
                    {
                        string dist_x_string = "0";
                        o.Properties.TryGetValue("dist_x", out dist_x_string);
                        string dist_y_string = "0";
                        o.Properties.TryGetValue("dist_y", out dist_y_string);
                        string movement_style_string;
                        if(!o.Properties.TryGetValue("movement_style", out movement_style_string))
                        {
                            movement_style_string = "linear";
                        }
                        string start_delay_string;
                        if (!o.Properties.TryGetValue("start_delay", out start_delay_string))
                        {
                            start_delay_string = "0";
                        }
                        string one_way_string;
                        if (!o.Properties.TryGetValue("one_way", out one_way_string))
                        {
                            one_way_string = "false";
                        }
                        platform p = new platform((float)o.X + ((float)o.Width * 0.5f), (float)o.Y + ((float)o.Height * 0.5f), (int)(o.Width / 8.0f), int.Parse(dist_x_string), int.Parse(dist_y_string), (platform.movement_style)Enum.Parse(typeof(platform.movement_style), movement_style_string), int.Parse(start_delay_string))
                        {
                            one_way = bool.Parse(one_way_string),
                        };
                        objs_add_queue.Add(p);
                    }
                    else if (string.Compare(o.Type, "spawn_push_block", true) == 0)
                    {
                        push_block block = new push_block()
                        {
                            x = (float)o.X + ((float)o.Width * 0.5f),
                            y = (float)o.Y + ((float)o.Height * 0.5f)
                        };
                        objs_add_queue.Add(block);
                    }
                    else if (string.Compare(o.Type, "map_link", true) == 0)
                    {
                        string require_up_press_string = "false";
                        bool require_up_press = false;
                        if (o.Properties.TryGetValue("require_up_press", out require_up_press_string))
                        {
                            require_up_press = bool.Parse(require_up_press_string);
                        }
                        map_link ml = new map_link()
                        {
                            x = (float)o.X + ((float)o.Width * 0.5f),
                            y = (float)o.Y + ((float)o.Height * 0.5f),
                            w = (int)o.Width,
                            h = (int)o.Height,
                            require_up_press = require_up_press,
                        };
                        ml.cw = ml.w;
                        ml.ch = ml.h;

                        string out_string;
                        if (o.Properties.TryGetValue("dest_map_path", out out_string))
                        {
                            ml.dest_map_path = out_string;
                        }
                        if (o.Properties.TryGetValue("dest_spawn_name", out out_string))
                        {
                            ml.dest_spawn_name = out_string;
                        }
                        if (o.Properties.TryGetValue("dir", out out_string))
                        {
                            ml.trans_dir = (map_link.transition_dir)Enum.Parse(typeof(map_link.transition_dir), out_string);
                        }

                        objs_add_queue.Add(ml);
                    }
                    else if (string.Compare(o.Type, "artifact", true) == 0)
                    {
                        artifacts t = (artifacts)Enum.Parse(typeof(artifacts), o.Properties["id"]);

                        // Has the player already found this?
                        if ((pc.found_artifacts & t) == 0)
                        {
                            artifact_pickup ap = new artifact_pickup(t)
                            {
                                x = (float)o.X + ((float)o.Width * 0.5f),
                                y = (float)o.Y + ((float)o.Height * 0.5f),
                            };

                            objs_add_queue.Add(ap);
                        }
                    }
                    else if (string.Compare(o.Type, "gem", true) == 0)
                    {
                        // New logic with id embedded in object.
                        string IDString = o.Properties["ID"];
                        gem_id = int.Parse(IDString);

                        //System.Diagnostics.Debug.Assert(cur_level_id >= 0);
                        //System.Diagnostics.Debug.Assert(gem_id < gems_per_level);

                        // Max of 4 gems per level.
                        //if (gem_id < gems_per_level)
                        {
                            //UInt32 gem_mask = (UInt32)1 << gem_id;// + (gems_per_level * cur_level_id);

                            //if ((inst.pc.found_gems_00 & gem_mask) == 0)
                            if (!inst.pc.is_gem_found(gem_id))
                            {
                                gem_pickup gem = new gem_pickup(gem_id)
                                {
                                    x = (float)o.X + ((float)o.Width * 0.5f),
                                    y = (float)o.Y + ((float)o.Height * 0.5f),
                                };

                                objs_add_queue.Add(gem);
                            }

                            gem_id++;
                        }
                    }
                }
            }

            objs_add_queue.Add(pc);

            if (pawn != null)
            {
                pc.possess(pawn);
            }

            //const int hud_height = 16;

            //// Account for the fact that the camera area can be smaller than the game resolution.
            //// This could be fixed in content by always setting a min cam size of ResX/Y, but this
            //// allows us to change the resolution without having to update content.
            //Vector2 cam_area_min_og = cam_area_min;
            //Vector2 cam_area_max_og = cam_area_max;
            //Vector2 cam_area = cam_area_max - cam_area_min;
            //Vector2 cam_delta_half = (Res - cam_area) * 0.5f;

            //// Is the camera area smaller than the resolution? If so, adjust it (while keeping
            //// it centered around the same point) to match the resolution.
            //// NOTE: At time of writing we still render the map outside the camera area.
            //if (cam_area.X < Res.X)
            //{
            //    cam_area_min.X -= cam_delta_half.X;
            //    cam_area_max.X += cam_delta_half.X;
            //}
            //if (cam_area.Y < Res.Y)
            //{
            //    cam_area_min.Y -= cam_delta_half.Y - 8;
            //    cam_area_max.Y += cam_delta_half.Y - 8;
            //}

            //game_cam = new cam(pc)
            //{
            //    pos_min = cam_area_min + new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f - hud_height),
            //    pos_max = cam_area_max - new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f),
            //    play_area_min = cam_area_min_og,
            //    play_area_max = cam_area_max_og,
            //    pull_threshold_offset = new Vector2(0, pull_threshold_offset_y),
            //};
            //if (cur_map_config.is_tower)
            //{
            //    game_cam.pull_threshold = 0;
            //}
            //game_cam.jump_to_target();

            game_cam_man._update60();
            if (game_cam_man.queued_cam != null)
            {
                game_cam = game_cam_man.queued_cam;
                game_cam_man.queued_cam = null;
                game_cam.jump_to_target();
            }

            foreach (PicoXObj o in objs_add_queue)
            {
                sprite s = o as sprite;
                if (s != null)
                {
                    s.x_initial = s.x;
                    s.y_initial = s.y;
                }
            }
        }

        public void set_game_state(game_state new_state)
        {
            // Used in the case of entering gameplay, both from transitioning maps,
            // and flow between states.
            Vector2 spawn_point = Vector2.Zero;

            // Leaving...
            switch (cur_game_state)
            {
                case game_state.main_menu:
                    {
                        break;
                    }
                case game_state.gameplay_dead:
                    {
                        if (new_state == game_state.game_over)
                        {
                            objs.Clear();
                            objs_remove_queue.Clear();
                            objs_add_queue.Clear();
                        }
                        break;
                    }

                case game_state.game_over:
                    {
                        // If we died we want to reload the PC to the previous state of the save game.
                        // Don't want to do this in 'entering...' section because that will get hit when
                        // moving between levels.
                        pc.reload();

                        if (new_state == game_state.level_trans_enter)
                        {
                            if (last_activated_checkpoint != null)
                            {
                                current_map = last_activated_checkpoint.map_name;
                                queued_map = current_map;
                                spawn_point.X = last_activated_checkpoint.cx;
                                spawn_point.Y = last_activated_checkpoint.cy;
                            }
                        }
                        break;
                    }
            }

            // When transitioning levels, we go through these transient "level_trans" states, but when we come out of
            // it we need to know what "core" state we came from because entering gameplay from the main menu is different from
            // entering gameplay from gameover, for instance.
            if (new_state != game_state.level_trans_enter && new_state != game_state.level_trans_exit)
            {
                prev_game_state = cur_game_state;
            }

            cur_game_state = new_state;
            time_in_state = 0;

            // Entering...
            switch (cur_game_state)
            {
                case game_state.main_menu:
                    {
                        // display the title screen map.
                        queued_map = current_map = "Content/raw/map_title_00.tmx";
                        initialize_map(ref spawn_point, false);

                        // main menu
                        ui_scene.add_child
                        (
                            new ui_menu_scene() { x = Res.X * 0.5f - 14.0f, y = Res.Y * 0.75f }
                            .add_child
                            (
                                new ui_text() { display_string = "new game", color = 7, color_outline = 5, outline = true }
                            )
                            .add_child
                            (
                                new ui_text() { display_string = "continue", color = 7, color_outline = 5, outline = true, y = 8 }
                            )
                        );
                        break;
                    }
                case game_state.game_win:
                    {
                        objs.Clear();
                        objs_remove_queue.Clear();
                        objs_add_queue.Clear();
                        break;
                    }
                case game_state.level_trans_exit:
                    {
                        break;
                    }
                case game_state.level_trans_enter:
                    {
                        if (prev_game_state == game_state.main_menu)
                        {
                            // The only state currently using the ui_scene is the main menu.
                            // Eventually this might need to be more intelligent about when
                            // and what gets cleared.
                            ui_scene.clear_children();

                            // right 16 bits = x
                            // left 16 bits = y
                            int pack_pos = dget((int)cartdata_index.overworld_pos_packed);

                            if (pack_pos != 0)
                            {
                                // Isolate the x and y components of the 32 bit value.
                                short unpacked_x = (short)(pack_pos & short.MaxValue);
                                short unpacked_y = (short)(pack_pos >> 16);

                                spawn_point.X = unpacked_x * 8.0f;
                                spawn_point.Y = unpacked_y * 8.0f;
                            }
                        }
                        initialize_map(ref spawn_point, false);
                        break;
                    }
                default:
                    break;
            }
        }

        public Point map_pos_to_meta_tile(int x, int y)
        {
            x = (x % 2 == 0) ? x : x - 1;
            y = (y % 2 == 0) ? y : y - 1;
            return new Point(x, y);
        }

        public void change_meta_tile(int x, int y, int[] t, int bank)
        {
            Point final_pos = map_pos_to_meta_tile(x, y);
            x = final_pos.X;
            y = final_pos.Y;

            int count = 0;

            for (int j = 0; j <= 1; j++)
            {
                for (int i = 0; i <= 1; i++)
                {
                    msetbank(x + i, y + j, t[count], bank);
                    count += 1;
                }
            }
        }

        public void change_meta_tile(int x, int y, int tile_id, int bank)
        {
            change_meta_tile(x, y, new int[] { tile_id, tile_id, tile_id, tile_id }, bank);
        }

        public int[] default_pal = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        public int[][] fade_table =
        {
            new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, // default
            new int[] { 0, 1, 2, 3, 4, 0, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15 },
            new int[] { 0, 1, 2, 3, 4, 0, 0, 5, 8, 9, 10, 11, 12, 13, 14, 15 },
            new int[] { 0, 1, 2, 3, 4, 0, 0, 0, 8, 9, 10, 11, 12, 13, 14, 15 }, // darkest
        };
        public int[][] bright_table =
        {
            new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, // default
            new int[] { 5, 1, 2, 3, 4, 6, 7, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            new int[] { 6, 1, 2, 3, 4, 7, 7, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            new int[] { 7, 1, 2, 3, 4, 7, 7, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, // brightest
        };
        public int[] shadow_pal = new int[16] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, };
        // blue tint
        public int[] default_pal_invert_fg = new int[] { 0, 1, 2, 3, 4, 13, 12, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        // red tint
        public int[] default_pal_invert_bg = new int[] { 2, 1, 2, 3, 4, 8, 9, 10, 8, 9, 10, 11, 12, 13, 14, 15 };

        public int[] get_cur_pal(bool is_fg)
        {
            int fade_darkness_level = 0;
            switch (cur_game_state)
            {
                case game_state.level_trans_exit:
                    {
                        int fade_step_time = level_trans_time / 3;
                        if (time_in_state < 0)
                        {

                        }
                        else if (time_in_state < fade_step_time)
                        {
                            fade_darkness_level = 1;
                        }
                        else if (time_in_state < fade_step_time * 2)
                        {
                            fade_darkness_level = 2;
                        }
                        else
                        {
                            fade_darkness_level = 3;
                        }
                        break;
                    }
                case game_state.level_trans_enter:
                    {
                        int fade_step_time = level_trans_time / 3;
                        if (time_in_state < fade_step_time)
                        {
                            fade_darkness_level = 3;
                        }
                        else if (time_in_state < fade_step_time * 2)
                        {
                            fade_darkness_level = 2;
                        }
                        else
                        {
                            fade_darkness_level = 1;
                        }
                        break;
                    }
            }

            int map_darkness_level = 0;
            if (!pc.has_artifact(artifacts.light))
            {
                map_darkness_level = cur_map_config.darkness_level;
            }

            if (map_darkness_level > 0 || fade_darkness_level > 0)
            {
                return fade_table[(int)max(map_darkness_level, fade_darkness_level)];
            }
            else
            {
                return default_pal;
            }
        }

        public void apply_pal(int[] p)
        {
            for (int i = 0; i <= 15; i++)
            {
                pal(i, p[i]);
            }
        }

        List<Tuple<string /*label*/, string/*value*/>> populate_map_list()
        {
            string targetDirectory = Directory.GetCurrentDirectory() + "/Content/raw/";

            // Process the list of files found in the directory.
            string[] map_file_list = Directory.GetFiles(targetDirectory, "*.tmx");

            List<Tuple<string /*label*/, string/*value*/>> data_set = new List<Tuple<string, string>>();
            int count = 1;
            foreach (string s in map_file_list)
            {
                string file = Path.GetFileName(s);
                if (file.StartsWith("map_") && !file.Contains("template"))
                {
                    data_set.Add(new Tuple<string, string>(file + " (" + count++ + "/" + map_file_list.Length + ")", "Content/raw/" + file));
                    printh("Add: " + file);
                }
                else
                {
                    printh("Ignore: " + file);
                }
            }

            return data_set;
        }

        public void printo(string str, float startx, float starty, int col, int col_bg)
        {
            print(str, startx + 1, starty, col_bg);
            print(str, startx - 1, starty, col_bg);
            print(str, startx, starty + 1, col_bg);
            print(str, startx, starty - 1, col_bg);
            print(str, startx + 1, starty - 1, col_bg);
            print(str, startx - 1, starty - 1, col_bg);
            print(str, startx - 1, starty + 1, col_bg);
            print(str, startx + 1, starty + 1, col_bg);
            print(str, startx, starty, col);
        }

        public bool btn_confirm()
        {
            return btn(4) || btn(6);
        }

        public bool btnp_confirm()
        {
            return btnp(4) || btnp(6);
        }

        public int sprfx_camo_warp(int x, int y, int c)
        {
            // Don't do anything with transparent pixels.
            // TODO: Check if c == palt value.
            if (c == 11) return c;

            // Dither:
            //if (x % 2 == 0 && y % 2 == 0 || x % 2 == 1 && y % 2 == 1)
            //{
            //    return 19;
            //}

            // Look at the color already at this location and use that color
            // but faded out slightly.
            int[] t = new int[] { 0, 5, 6, 7 };
            return t[flr(rnd(4))];// fade_table[1][pget(x, y)];
        }



        public int sprfx_fade(int x, int y, int c)
        {
            // Don't do anything with transparent pixels.
            // TODO: Check if c == palt value.
            if (c == 11 || c == 7) return c;

            // Dither:
            //if (x % 2 == 0 && y % 2 == 0 || x % 2 == 1 && y % 2 == 1)
            //{
            //    return 19;
            //}

            // Look at the color already at this location and use that color
            // but faded out slightly.
            //int[] t = new int[] { 0, 5, 6, 7 };
            return fade_table[1][pget(x, y)];
        }

        public int sprfx_tower_fade(int x, int y, int c)
        {
            if (c == 11 || inst.pc.pawn == null) return c;

            float tower_width = 192.0f * 0.5f;

            // Note: x,y come in a world position. It looks like screen position if you look at the code,
            //       however, pset applies camera transforms.      

            // Find the distance from the player to the pixel (since the player is always centered). Could probably also
            // use the camera position.
            // The distance can loop at the x edges, so we get the distance in both directions,
            // and take the smaller one (imagine player is sitting at right edge 383, while the pixel is at 0.
            float delta = min(abs((inst.pc.pawn.x + cur_map_config.map_size_pixels.X) - x), min(abs((inst.pc.pawn.x - cur_map_config.map_size_pixels.X) - x), abs(inst.pc.pawn.x - x)));

            //delta = abs(x - (Res.X * 0.5f));

            var tabl = fade_table;

            if (delta >= tower_width * 1.025f)
            {
                return 11;
            }
            else if (delta > tower_width * 0.9f)
            {
                return tabl[3][c];
            }
            else if (delta > tower_width * 0.8f)
            {
                if (x % 2 == 0 && y % 2 == 0 || x % 2 == 1 && y % 2 == 1)
                {
                    return tabl[2][c];
                }
                return tabl[3][c];
            }
            else if (delta > tower_width * 0.6f)
            {
                if (x % 2 == 0 && y % 2 == 0 || x % 2 == 1 && y % 2 == 1)
                {
                    return tabl[1][c];
                }
                return tabl[2][c];
            }
            else if (delta > tower_width * 0.5f)
            {
                // Dither:
                if (x % 2 == 0 && y % 2 == 0 || x % 2 == 1 && y % 2 == 1)
                {
                    return c;
                }
                return tabl[1][c];
            }

            return c;
        }


        List<Tuple<string /*label*/, string/*value*/>> debug_map_list;
        int cur_debug_map = -1;

        BufferedKey next_level_key = new BufferedKey(Keys.PageDown);
        BufferedKey prev_level_key = new BufferedKey(Keys.PageUp);
        BufferedKey next_tenth_level_key = new BufferedKey(new Keys[] { Keys.LeftShift, Keys.PageDown });
        BufferedKey prev_tenth_level_key = new BufferedKey(new Keys[] { Keys.LeftShift, Keys.PageUp });
        BufferedKey ReloadContentButton = new BufferedKey(new Keys[] { Keys.LeftShift, Keys.R});
        BufferedKey toggle_debug_draw = new BufferedKey(Keys.F1);
        BufferedKey toggle_fly = new BufferedKey(Keys.F);

        BufferedKey toggle_artifact_00 = new BufferedKey(Keys.D1);
        BufferedKey toggle_artifact_01 = new BufferedKey(Keys.D2);
        BufferedKey toggle_artifact_02 = new BufferedKey(Keys.D3);
        BufferedKey toggle_artifact_03 = new BufferedKey(Keys.D4);
        BufferedKey toggle_artifact_04 = new BufferedKey(Keys.D5);
        BufferedKey toggle_artifact_05 = new BufferedKey(Keys.D6);
        BufferedKey toggle_all_gems = new BufferedKey(Keys.D0);

        int update_counter = 0;

        public class map_config
        {
            public int darkness_level = 0;
            // The size (in pixels) of the currently loaded map.
            public Point map_size_pixels = Point.Zero;
            // Is the current map a rotating tower type.
            public bool is_tower = false;
        }

        public map_config cur_map_config = new map_config();

        public class timer_callback : PicoXObj
        {
            int delay;
            Action callback;

            public timer_callback(int delay, Action callback)
            {
                this.delay = delay;
                this.callback = callback;

            }

            public override void _update60()
            {
                base._update60();

                delay--;

                if (delay <= 0)
                {
                    callback();
                    inst.objs_remove_queue.Add(this);
                }
            }
        }

        // A global scene used for displaying all ui_widgets. This an empty root.
        ui_widget ui_scene;

        struct star
        {
            public Point pos;
            public int color;
            public bool big;
        }

        star[] stars = new star[128];

        public int cur_save_version = 1;
        public void clear_save()
        {
            // Zero's out all cartdata. Could do more complex logic if needed.
            for (uint i = 0; i < 64; i++)
            {
                dset(i, 0);
            }

            // version should be set, even on a clear save.
            dset((uint)cartdata_index.version, cur_save_version);
        }

        public override void _init()
        {
            debug_map_list = populate_map_list();

            // Create save file.
            cartdata("mbh-platformer");

            // Zero's out all cartdata. Could do more complex logic if needed.
            Action clear_save_del = delegate ()
            {
                clear_save();
            };

            // Add ability for user to clear save data.
            menuitem(1, "clear save", clear_save_del);

            int ver = dget((uint)cartdata_index.version);

            // If this is an old version, clear it.
            // Ideally this would not just clear the save but rather "upgrade it".
            if (ver < cur_save_version)
            {
                clear_save();
            }

            sprfxadd(sprfx_camo_warp, 0);
            sprfxadd(sprfx_tower_fade, 1);
            sprfxadd(sprfx_fade, 2);

            objs = new List<PicoXObj>();
            objs_remove_queue = new List<PicoXObj>();
            objs_add_queue = new List<PicoXObj>();
            // one player controller for the life of the game.
            pc = new player_controller();
            start_game = new complex_button(4);
            hit_pause = new hit_pause_manager();
            
            ui_scene = new ui_widget();
            //ui_scene.
            //    add_child
            //    (
            //        new ui_widget() { x = 32, y = 32 }.
            //        add_child
            //        (
            //            new ui_box() {x = -2, y = -2, width = 40, height = 16, color = 5 }
            //        ).
            //        add_child
            //        (
            //            new ui_text() { color = 7, display_string = "new game" }
            //        ).
            //        add_child
            //        (
            //            new ui_text() { color = 7, y = 8, display_string = "continue"}
            //        )
            //    );

            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new star()
                {
                    pos = new Point(flr(rnd(Res.X)), flr(rnd(Res.Y))),
                    big = rnd(10) > 1 ? false : true,
                    color = rnd(10) > 1 ? 5 : 7,
                };
            }

            set_game_state(game_state.main_menu);
        }

        public override void _update60()
        {
            update_counter++;
            time_in_state++;

            start_game._update60();

            if (ReloadContentButton.Update())
            {
                Vector2 spawn_point = Vector2.Zero;
                initialize_map(ref spawn_point, true);
            }

#if DEBUG
            if (toggle_debug_draw.Update())
            {
                debug_draw_enabled = !debug_draw_enabled;
            }

            if (toggle_fly.Update())
            {
                pc.DEBUG_fly_enabled = !pc.DEBUG_fly_enabled;
            }

            if (toggle_artifact_00.Update())
            {
                inst.pc.found_artifacts ^= artifacts.dash_pack;
            }

            if (toggle_artifact_01.Update())
            {
                inst.pc.found_artifacts ^= artifacts.jump_boots;
            }

            if (toggle_artifact_02.Update())
            {
                inst.pc.found_artifacts ^= artifacts.rock_smasher;
            }

            if (toggle_artifact_03.Update())
            {
                inst.pc.found_artifacts ^= artifacts.ground_slam;
            }

            if (toggle_artifact_04.Update())
            {
                inst.pc.found_artifacts ^= artifacts.light;
            }

            if (toggle_artifact_05.Update())
            {
                inst.pc.found_artifacts ^= artifacts.air_tank;
            }

            if (toggle_all_gems.Update())
            {
                if (inst.pc.get_gem_count() > 0)
                {
                    inst.pc.found_gems_00 = 0;
                    inst.pc.found_gems_01 = 0;
                }
                else
                {
                    inst.pc.found_gems_00 = UInt32.MaxValue;
                    inst.pc.found_gems_01 = UInt32.MaxValue;
                }
            }
#endif

            switch (cur_game_state)
            {
                case game_state.level_trans_exit:
                    {
                        hit_pause.start_pause(hit_pause_manager.pause_reason.level_trans);
                        if (time_in_state >= level_trans_time * 2)
                        {
                            set_game_state(game_state.level_trans_enter);
                        }
                        break;
                    }
                case game_state.level_trans_enter:
                    {
                        hit_pause.start_pause(hit_pause_manager.pause_reason.level_trans);
                        if (time_in_state >= level_trans_time)
                        {
                            active_map_link = null;
                            set_game_state(game_state.gameplay);
                        }
                        break;
                    }
                case game_state.gameplay:
                    {
                        //if (time_in_state % 120 == 0)
                        //{
                        //    objs_add_queue.Add(new lava_blast_spawner() { x = 29 * 8, y = 93 * 8 });
                        //}
                        //if (time_in_state % 120 == 0)
                        //{
                        //    game_cam.shake((int)rnd(30) + 30, 1);
                        //}

                        if (game_cam_man.queued_cam != null)
                        {
                            hit_pause.start_pause(hit_pause_manager.pause_reason.level_trans);
                        }
#if DEBUG
                        if (next_tenth_level_key.Update())
                        {
                            int index = debug_map_list.FindIndex((Tuple<string, string> param) => { return (param.Item2 == current_map); });
                            if (index >= 0)
                            {
                                cur_debug_map = (index + 10) % debug_map_list.Count;
                                queued_map = debug_map_list[cur_debug_map].Item2;
                                printh("queued: " + queued_map);
                            }
                            // update the key since we won't hit the else.
                            next_level_key.Update();
                        }
                        else if (next_level_key.Update())
                        {
                            int index = debug_map_list.FindIndex((Tuple<string,string> param) => { return (param.Item2 == current_map); });
                            if (index >= 0)
                            {
                                cur_debug_map = (index + 1) % debug_map_list.Count;
                                queued_map = debug_map_list[cur_debug_map].Item2;
                                printh("queued: " + queued_map);
                            }
                        }
                        if (prev_tenth_level_key.Update())
                        {
                            int index = debug_map_list.FindIndex((Tuple<string, string> param) => { return (param.Item2 == current_map); });
                            if (index >= 0)
                            {
                                cur_debug_map = ((index - 10) + debug_map_list.Count) % debug_map_list.Count;
                                queued_map = debug_map_list[cur_debug_map].Item2;
                                printh("queued: " + queued_map);
                            }
                            // update the key since we won't hit the else.
                            prev_level_key.Update();
                        }
                        else if (prev_level_key.Update())
                        {
                            int index = debug_map_list.FindIndex((Tuple<string, string> param) => { return (param.Item2 == current_map); });
                            if (index >= 0)
                            {
                                cur_debug_map = ((index - 1) + debug_map_list.Count) % debug_map_list.Count;
                                queued_map = debug_map_list[cur_debug_map].Item2;
                                printh("queued: " + queued_map);
                            }
                        }
#endif // DEBUG
                        break;
                    }
                case game_state.gameplay_dead:
                    {
                        if (time_in_state >= 120)
                        {
                            set_game_state(game_state.game_over);
                        }
                        break;
                    }
                case game_state.game_over:
                    {
                        if (start_game.is_released)
                        {
                            set_game_state(game_state.level_trans_enter);
                        }
                        break;
                    }
            }

            if (message != null)
            {
                hit_pause.start_pause(hit_pause_manager.pause_reason.message_box_open);
                message.open_this_frame = false;
            }

            // flip before iterating backwards. This is slight wrong, since hidden items will get flipped
            // multiple times.
            // NOTE: This comes BEFORE objects get updated to avoid case where an object is
            //       added to objs list, but isn't updated, and then gets drawn with garbage
            //       data.
            objs_add_queue.Reverse();
            for (int i = objs_add_queue.Count - 1; i >= 0; i--)
            {
                sprite s = objs_add_queue[i] as sprite;

                if (s != null)
                {
                    if (inst.is_packed_tile(fget(mget_tiledata(flr(s.x / 8.0f), flr(s.y / 8.0f))), packed_tile_types.rock_smash))
                    {
                        continue;
                    }
                }
                objs.Add(objs_add_queue[i]);
                objs_add_queue.RemoveAt(i);
            }

            // TODO: Should we ignore objects in the remove queue?
            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._preupdate();
            }
            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._update60();
            }
            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._postupdate();
            }

            // Remove all the objects which requested to be removed.
            objs = objs.Except(objs_remove_queue).ToList();
            objs_remove_queue.Clear();

            // flip before iterating backwards. This is slight wrong, since hidden items will get flipped
            // multiple times.
            //objs_add_queue.Reverse();

            //for (int i = objs_add_queue.Count - 1; i >= 0; i--)
            //{
            //    sprite s = objs_add_queue[i] as sprite;

            //    if (s != null)
            //    {
            //        if (inst.is_packed_tile(fget(mget_tiledata(flr(s.x / 8.0f), flr(s.y / 8.0f))), packed_tile_types.rock_smash))
            //        {
            //            continue;
            //        }
            //    }
            //    objs.Add(objs_add_queue[i]);
            //    objs_add_queue.RemoveAt(i);
            //}

            //objs.AddRange(objs_add_queue);
            //objs_add_queue.Clear();

            game_cam_man._update60();
            if (game_cam != null)
            {
                game_cam._update60();
            }
            if (hit_pause != null)
            {
                hit_pause._update60();
            }

            if (message != null && !message.open_this_frame)
            {
                if (btnp_confirm())
                {
                    message.on_close_delegate?.Invoke();
                    message = null;
                }
            }

            ui_scene._update60();

            if ((active_map_link != null || queued_map != GetMapString()) && cur_game_state != game_state.level_trans_exit && cur_game_state != game_state.level_trans_enter)
            {
                set_game_state(game_state.level_trans_exit);
            }
        }

        public override void _draw()
        {
            pal();
            palt(0, false);
            palt(11, true);
            cls(0);

            Vector2 final_cam = Vector2.Zero;
            Vector2 offset = Vector2.Zero;

            if (game_cam != null)
            {

                if (active_map_link != null && active_map_link.trans_dir != map_link.transition_dir.none)
                {
                    if (cur_game_state == game_state.level_trans_exit && time_in_state > level_trans_time)
                    {
                        float time = time_in_state - level_trans_time;
                        float amount = (float)(time) / (float)(level_trans_time);
                        if (active_map_link.trans_dir == map_link.transition_dir.horz)
                        {
                            float cam_x = game_cam.cam_pos().X + Res.X * 0.5f;
                            amount *= (pc.pawn.cx - cam_x);
                            offset.X += amount;
                        }
                        else
                        {
                            float cam_y = game_cam.cam_pos().Y + Res.Y * 0.5f;
                            amount *= (pc.pawn.cy - cam_y);
                            offset.Y += amount;
                        }
                    }
                }

                if (active_map_link != null && active_map_link.trans_dir != map_link.transition_dir.none)
                {
                    if (cur_game_state == game_state.level_trans_enter)// && time_in_state > level_trans_time)
                    {
                        float time = time_in_state;// - level_trans_time;
                        float amount = 1 - ((float)(time) / (float)(level_trans_time));
                        if (active_map_link.trans_dir == map_link.transition_dir.horz)
                        {
                            float cam_x = game_cam.cam_pos().X + Res.X * 0.5f;
                            amount *= (pc.pawn.cx - cam_x);
                            offset.X += amount;
                        }
                        else
                        {
                            float cam_y = game_cam.cam_pos().Y + Res.Y * 0.5f;
                            amount *= (pc.pawn.cy - cam_y);
                            offset.Y += amount;
                        }
                    }
                }

                if (game_cam_man.queued_cam != null)
                {
                    float amount = (float)(game_cam_man.queued_ticks) / (float)(level_trans_time * 3);

                    Vector2 delta = game_cam_man.queued_cam.cam_pos() - game_cam.cam_pos();
                    offset = Vector2.SmoothStep(Vector2.Zero, delta, amount);
                    //offset = delta * 0.1f;
                    if (amount >= 1.0f)
                    {
                        offset = Vector2.Zero;
                        game_cam = game_cam_man.queued_cam;
                        game_cam_man.queued_cam = null;
                    }
                }

                final_cam = new Vector2(game_cam.cam_pos().X + offset.X, game_cam.cam_pos().Y + offset.Y);
            }

            bool draw_left = final_cam.X < Res.X * 0.5f;

            camera(final_cam.X, final_cam.Y);

            if (cur_map_config.is_tower && pc.pawn != null)
            {
                apply_pal(get_cur_pal(false));

                camera(0, 0);

                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i].big)
                    {
                        circ(((stars[i].pos.X + game_cam.cam_pos().X) + Res.X) % Res.X, stars[i].pos.Y, 1, stars[i].color);
                    }
                    else
                    {
                        pset(((stars[i].pos.X + game_cam.cam_pos().X) + Res.X) % Res.X, stars[i].pos.Y, stars[i].color);
                    }
                }

                int half_width = 96;

                int tower_left_x = (int)pc.pawn.x - half_width; // (int)((Res.X * 0.5f) - (half_width));
                int tower_offset = (-(int)((game_cam.cam_pos().X * 0.5f)) % 16) + 16;

                /*
                //bset(1);
                //for(int x = 0; x < Res.X / 16 + 2; x++)
                //{
                //    for (int y = 0; y < Res.Y / 16; y++)
                //    {
                //        sspr(0, 0, 16, 16, pc.pawn.x - (Res.X * 0.5f) + (x * 16.0f) - tower_offset, y * 16);
                //    }
                //}
                
                float speed = 1;// 0.25f;
                camera(0, 0);
                //for (int i = 0; i < 3; i++)
                {
                    //map(0, 0, final_cam.X * 2, 0, 9999, 30, 0, 3);
                    if (!draw_left)
                    {
                        //camera((-final_cam.X * speed + cur_map_config.map_size_pixels.X), final_cam.Y);
                        map(0, 0, cur_map_config.map_size_pixels.X + final_cam.X, 0, 9999, 30, 0, 3);
                    }
                    else
                    {
                        ///camera((-final_cam.X * speed - cur_map_config.map_size_pixels.X), final_cam.Y);
                        map(0, 0, -cur_map_config.map_size_pixels.X + final_cam.X, 0, 9999, 30, 0, 3);
                    }
                    //camera(-final_cam.X * speed, final_cam.Y);
                    map(0, 0, +final_cam.X, 0, 9999, 30, 0, 3);
                    //map(0, i * 10, 0, 0, 56, 10, 0, 3);
                    //speed += 0.5f;
                }
                */

                // Mod by the height of 2 tiles (eg. a single loop below) to keep the tower on screen at all times.
                // Every other row it loops, so this looks seamless.
                camera(final_cam.X, final_cam.Y % 32);
                bset(4);

                // An extra 2 rows to account of for looping logic above (mod 32).
                for (int y = 0; y < (Res.Y / 16) + 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        int y_offset = 0;
                        if (y % 2 == 0)
                        {
                            y_offset = 8;
                        }
                        sspr(0, (int)((tower_offset + y_offset) % 16) * 16, half_width, 16, tower_left_x, y * 16);//  32, x, y * 16, 12, 2);
                        sspr(0, (int)((tower_offset + y_offset) % 16) * 16 + 256, half_width, 16, tower_left_x + half_width, y * 16);//  32, x, y * 16, 12, 2);
                    }
                }
                bset(0);
                pal();
            }

            if (cur_map_config.is_tower)
            {
                sprfxset(1, true);
            }

            camera(final_cam.X, final_cam.Y);

            switch (cur_game_state)
            {
                case game_state.main_menu:
                case game_state.level_trans_exit:
                case game_state.level_trans_enter:
                case game_state.gameplay:
                case game_state.gameplay_dead:
                    {
                        //pal(7, 0);
                        //pal(6, 5);
                        //pal(5, 6);
                        //pal(0, 7);
                        apply_pal(get_cur_pal(false));
                        // Assume a max of 3 layers for now. Missing or "not visible" layers will not be rendered.
                        map(0, 0, 0, 0, 9999, 9999, 0, 0);
                        map(0, 0, 0, 0, 9999, 9999, 0, 1);
                        //map(0, 0, 0, 0, 9999, 9999, 0, 2);
                        if (cur_map_config.is_tower)
                        {
                            if (draw_left)
                            {
                                camera((final_cam.X + cur_map_config.map_size_pixels.X) % cur_map_config.map_size_pixels.X, final_cam.Y);
                                map(0, 0, 0, 0, 9999, 9999, 0, 0);
                                //map(0, 0, 0, 0, 9999, 9999, 0, 1);
                                //map(0, 0, 0, 0, 9999, 9999, 0, 2);
                            }
                            else
                            {
                                camera((final_cam.X - cur_map_config.map_size_pixels.X) % cur_map_config.map_size_pixels.X, final_cam.Y);
                                map(0, 0, 0, 0, 9999, 9999, 0, 0);
                                //map(0, 0, 0, 0, 9999, 9999, 0, 1);
                                //map(0, 0, 0, 0, 9999, 9999, 0, 2);
                            }
                        }
                        bset(0);
                        pal();
                        //map(0, 0, 0, 0, 16, 16, 0, 1); // easy mode?
                        //pal();
                        break;
                    }
            }
            sprfxset(1, false);

            if (cur_map_config.is_tower)
            {
                sprfxset(1, true);
            }

            foreach (PicoXObj o in objs)
            {
                if (o is sprite)
                {
                    if (game_cam == null || game_cam.is_obj_in_play_area(o as sprite, offset))
                    {
                        (o as sprite).push_pal();
                        o._draw();
                        (o as sprite).pop_pal();
                    }
                }
            }

            if (cur_map_config.is_tower)
            {
                camera((final_cam.X + cur_map_config.map_size_pixels.X) % cur_map_config.map_size_pixels.X, final_cam.Y);
                foreach (PicoXObj o in objs)
                {
                    if (o is sprite)
                    {
                        (o as sprite).push_pal();
                        o._draw();
                        (o as sprite).pop_pal();
                    }
                }
                camera((final_cam.X - cur_map_config.map_size_pixels.X) % cur_map_config.map_size_pixels.X, final_cam.Y);
                foreach (PicoXObj o in objs)
                {
                    if (o is sprite)
                    {
                        (o as sprite).push_pal();
                        o._draw();
                        (o as sprite).pop_pal();
                    }
                }
            }
            sprfxset(1, false);

            // draw foreground layer.
            switch (cur_game_state)
            {
                case game_state.main_menu:
                case game_state.level_trans_exit:
                case game_state.level_trans_enter:
                case game_state.gameplay:
                case game_state.gameplay_dead:
                    {
                        apply_pal(get_cur_pal(false));
                        map(0, 0, 0, 0, 9999, 9999, 0, 2);
                        bset(0);
                        pal();
                        break;
                    }
            }

            // Draw the player here so that it draws over the fade out during level transition.
            //apply_pal(get_cur_pal(true));
            //if (pc.pawn != null)
            //{
            //    pc.pawn.push_pal();
            //    pc.pawn._draw();
            //    pc.pawn.pop_pal();
            //}
            //pal();

            if (game_cam != null)
            {
                game_cam._draw();
            }

            // Map grid.
            if (debug_draw_enabled)
            {
                int start_x = flr(final_cam.X / 16.0f) * 16;
                int start_y = flr(final_cam.Y / 16.0f) * 16;
                for (int x = start_x; x <= start_x + Res.X; x+=16)
                {
                    line(x, start_y, x, start_y + Res.Y + 16, 2);
                }
                for (int y = start_y; y <= start_y + Res.Y; y += 16)
                {
                    line(start_x, y, start_x + Res.X + 16, y, 2);
                }
            }

            // HUD

            camera(0, 0);

            //if (night_vision_enabled || ir_vision_enabled)
            if (false)
            {
                player_pawn p = pc.pawn;
                if (p != null)
                {
                    Vector2 v = new Vector2(p.x - final_cam.X /*+ (Res.X * 0.5f) + (rnd(dist_rnd) - (dist_rnd * 0.5f))*/, p.y - final_cam.Y /*+ (Res.Y * 0.5f) + (rnd(dist_rnd) - (dist_rnd * 0.5f))*/);

                    float size = 200.0f - sin(time_in_state * 0.011f) * 1.0f;
                    float max_dist = size*size;

                    rectfill(0, 0, Res.X, v.Y - size, 0);
                    rectfill(0, v.Y + size, Res.X, Res.Y, 0);
                    rectfill(0, v.Y - size, v.X - size, v.Y + size, 0);
                    rectfill(v.X + size, v.Y - size, Res.X, v.Y + size, 0);

                    // vignette
                    for (int x = (int)v.X - (int)size; x < v.X + size; x++)
                    {
                        for (int y = (int)v.Y - (int)size; y < v.Y + size; y++)
                        {

                            //float dist_rnd = 64;
                            //if (game_world.cur_area.light_status == area.lighting_status.on)
                            //{
                            //    dist_rnd = 64;
                            //}
                            //Vector2 v = new Vector2(p.x - game_cam.cam_pos().X /*+ (Res.X * 0.5f) + (rnd(dist_rnd) - (dist_rnd * 0.5f))*/, p.y - game_cam.cam_pos().Y /*+ (Res.Y * 0.5f) + (rnd(dist_rnd) - (dist_rnd * 0.5f))*/);
                            //Vector2 v = new Vector2(63 + (rnd(16) - 8), 63 + (rnd(16) - 8));
                            //Vector2 vf = v + new Vector2((rnd(dist_rnd) - (dist_rnd * 0.5f)));
                            float dist = Vector2.DistanceSquared(v, new Vector2(x, y));

                            //int fade_index = (int)MathHelper.SmoothStep(0, 3, dist / max_dist);
                            int fade_index = (int)mid(0, (dist / max_dist) * 4.0f, 3);
                            //if (rnd(100) < 75.0f)
                            //{
                            //    fade_index = MathHelper.Clamp(fade_index + ((int)rnd(3) - 1), 0, 7);
                            //}
                            if (fade_index != 0)
                            {
                                pset(x, y, fade_table[fade_index][pget(x, y)]);
                            }
                        }
                    }
                }

                // scanline
                //int min_y = (int)(((time_in_state * 0.5f) % 128.0f));

                //int max_y = min_y + 3;

                //for (int y = min_y; y < max_y; y++)
                //{
                //    float y_final = y % 128;

                //    float d = sin(((y_final * cos(time_in_state * 0.1f)) + time_in_state * 2) * 0.01f) * 127;

                //    for (int x = (int)abs(d); x > 0; x--)
                //    {
                //        float x_samp = x - cos(time_in_state * 0.1f);
                //        pset(x, y_final, pget((int)x_samp, (int)y_final));
                //    }
                //}
            }

            Action draw_health = () =>
            {
                if (pc == null || pc.pawn == null)
                {
                    return;
                }

                int y_pos = 1;

                for (int i = 0; i < pc.pawn.get_hp_max(); i++)
                {
                    int id = 238; // empty
                    if (i < flr(pc.pawn.hp))
                    {
                        id = 230;
                    }
                    else if (i > pc.pawn.hp)
                    {
                        id = 238;
                    }
                    else
                    {
                        float remainder = pc.pawn.hp - flr(pc.pawn.hp);
                        if (remainder > 0.75f)
                        {
                            id = 230; // full
                        }
                        else if (remainder > 0.5f)
                        {
                            id = 232; // 3/4
                        }
                        else if (remainder > 0.25f)
                        {
                            id = 234; // 2/4
                        }
                        else if (remainder > 0.0f)
                        {
                            id = 236; // 1/4
                        }
                        else
                        {
                            id = 238; // empty
                        }
                    }
                    apply_pal(get_cur_pal(false));
                    spr(id, y_pos, 1, 2, 2);
                    pal();
                    y_pos += 16;
                }
            };

            Action draw_gems = () =>
            {
                apply_pal(get_cur_pal(false));
                spr(260, Res.X - 32, 0, 2, 2);
                printo("x" + pc.get_gem_count(), Res.X - 14, 6, 7, 0);
                pal();
            };

            Action draw_hud = () =>
            {
                if (pc == null || pc.pawn == null)
                {
                    return;
                }
                //sprfxset(1, true);
                //apply_pal(get_cur_pal(false));
                rectfill(0, 0, Res.X, 15, 7);

                //for (int y = 0; y < 16; y++)
                //{
                //    for (int x = 0; x < Res.X; x++)
                //    {
                //        pset(x, y, fade_table[2][pget(x, y)]);
                //    }
                //}
                line(0, 16, Res.X, 16, 0);

                draw_health();
                draw_gems();
                //pal();
                //sprfxset(1, false);
            };

            //int step = 1;

            switch (cur_game_state)
            {
                case game_state.main_menu:
                    {
                        //var str = "dash maximus";
                        //print(str, (Res.X * 0.5f) - (str.Length * 0.5f) * 4, Res.Y * 0.5f, 7);
                        //str = "-dx-";
                        //print(str, (Res.X * 0.5f) - (str.Length * 0.5f) * 4, Res.Y * 0.5f + 6, 7);
                        break;
                    }
                case game_state.gameplay:
                    {
                        draw_hud();
                        if (debug_draw_enabled)
                        {
                            if (time_in_state < 500)
                            {
                                printo(Path.GetFileNameWithoutExtension(current_map).ToLower(), 1, Res.Y - 12, 7, 0);
                            }
                        }
                        break;
                    }
                case game_state.level_trans_exit:
                case game_state.level_trans_enter:
                    {
                        draw_hud();
                        break;
                    }
                case game_state.gameplay_dead:
                    {
                        const int initial_delay = 60;
                        const int ticks_per_step = 20;
                        draw_hud();
                        if (time_in_state < initial_delay)
                        {
                            apply_pal(fade_table[0]);
                        }
                        else if (time_in_state < initial_delay + ticks_per_step)
                        {
                            apply_pal(fade_table[1]);
                        }
                        else if (time_in_state < initial_delay + ticks_per_step * 2)
                        {
                            apply_pal(fade_table[2]);
                        }
                        else
                        {
                            apply_pal(fade_table[3]);
                        }
                        break;
                    }
                case game_state.game_over:
                    {
                        var str = "game over";
                        print(str, (Res.X * 0.5f) - (str.Length * 0.5f) * 4, (Res.Y *0.5f), 7);
                        break;
                    }
                case game_state.game_win:
                    {
                        var str = "you win! the galaxy is at peace";
                        print(str, (Res.X * 0.5f) - (str.Length * 0.5f) * 4, (Res.Y * 0.5f), 7);
                        break;
                    }
            }

            //step = (int)max(step, 1);

            //for (int x = 0; x < Res.X; x += step)
            //{
            //    for (int y = 0; y < Res.Y; y += step)
            //    {
            //        int color = pget(x, y);

            //        for (int i = 0; i < step; i++)
            //        {
            //            for (int j = 0; j < step; j++)
            //            {
            //                pset(x + i, y + j, color);
            //            }
            //        }
            //    }
            //}

            // message box
            if (message != null)
            {
                float box_w = message.longest_line * 4.0f; // Res.X / 2.0f;
                if (message.body_with_breaks.Count == 1)
                {
                    box_w = message.body_with_breaks[0].Length * 4;
                }
                float box_h = 6 * message.body_with_breaks.Count + 4;

                float x = (Res.X / 2) - (box_w / 2);
                float y = (Res.Y / 2) - (box_h / 2);

                rectfill(x, y, x + box_w, y + box_h, 0);
                rect(x + 1, y + 1, x + 1 + box_w - 2, y + 1 + box_h - 2, 7);

                for (int i = 0; i < message.body_with_breaks.Count; i++)
                {
                    print(message.body_with_breaks[i], x + 3, (y + 3) + (i * 6), 7);
                }
            }

            // Drawn in "hud-space" for the time being.
            ui_scene._draw();

            if (debug_draw_enabled)
            {
                string btnstr = "";
                for (int i = 0; i < 6; i++)
                {
                    btnstr += btn(i) ? "1" : "0";
                    btnstr += " ";
                }

                print(btnstr, 0, Res.Y - 5, 0);

                print(objs.Count.ToString(), btnstr.Length * 4, Res.Y - 5, 1);

                int ypos = 18;
                int yinc = 8;
                printo("dp:" + (((pc.found_artifacts & artifacts.dash_pack) == artifacts.dash_pack) ? "1" : "0"), 2, ypos, 7, 0);
                ypos += yinc;
                printo("jb:" + (((pc.found_artifacts & artifacts.jump_boots) == artifacts.jump_boots) ? "1" : "0"), 2, ypos, 7, 0);
                ypos += yinc;
                printo("rs:" + (((pc.found_artifacts & artifacts.rock_smasher) == artifacts.rock_smasher) ? "1" : "0"), 2, ypos, 7, 0);
                ypos += yinc;
                printo("gs:" + (((pc.found_artifacts & artifacts.ground_slam) == artifacts.ground_slam) ? "1" : "0"), 2, ypos, 7, 0);
                ypos += yinc;
                printo("lt:" + (((pc.found_artifacts & artifacts.light) == artifacts.light) ? "1" : "0"), 2, ypos, 7, 0);
                ypos += yinc;
                printo("at:" + (((pc.found_artifacts & artifacts.air_tank) == artifacts.air_tank) ? "1" : "0"), 2, ypos, 7, 0);
                ypos += yinc;
                printo("coin:" + pc.get_gem_count(), 2, ypos, 7, 0);
                ypos += yinc;
            }

            if (update_counter > 1)
            {
                printo("frames skipped: " + (update_counter-1), 32, 32, 8, 0);
            }
            update_counter = 0;

            //printo("cam: [" + final_cam.X + "," + final_cam.Y + "]", 32, 40, 9, 0);
        }

        public override string GetMapString()
        {
            return current_map;
        }

        public override Dictionary<int, string> GetMusicPaths()
        {
            return new Dictionary<int, string>();
        }

        public override List<string> GetSheetPath()
        {
            return new List<string>() { @"raw\platformer_sheet", @"raw\platformer_sheet_1", @"raw\platformer_sheet_2", @"raw\platformer_sheet_3", @"raw\platformer_sheet_4", @"raw\platformer_sheet_5", };
        }

        public override Dictionary<int, string> GetSoundEffectPaths()
        {
            return new Dictionary<int, string>();
        }

        public override Dictionary<string, object> GetScriptFunctions()
        {
            Dictionary<string, object> Funcs = new Dictionary<string, object>();
            Action toggle_fly = new Action(() => { pc.DEBUG_fly_enabled = !pc.DEBUG_fly_enabled; });
            Funcs.Add("fly", toggle_fly);
            Action call_ship = new Action(() =>
            {
                foreach(PicoXObj o in objs)
                {
                    if (o.GetType() == typeof(rocket_ship))
                    {
                        rocket_ship r = o as rocket_ship;
                        r.x = r.dest_x = pc.pawn.x;
                        r.y = r.dest_y = pc.pawn.y;
                        break;
                    }
                }
            });
            Funcs.Add("call_ship", call_ship);
            Action god = new Action(() =>
            {
                pc.DEBUG_god_enabled = !pc.DEBUG_god_enabled;
            });
            Funcs.Add("god", god);
            return Funcs;
        }

        public override string GetPalTextureString()
        {
            return "";
        }

        // note: we want to make sure the width and height at multiples of 16 to ensure tiles go right to the edge.
        public Vector2 Res = new Vector2(448, 240); // NES WS
        //public Vector2 Res = new Vector2(256, 240); // NES
        //public Vector2 Res = new Vector2(160, 144); // GB
        //public Vector2 Res = new Vector2(256, 144); // GB WS

        public override Tuple<int, int> GetResolution() { return new Tuple<int, int>((int)Res.X, (int)Res.Y); }

        public override int GetGifScale() { return 2; }

        public override bool GetGifCaptureEnabled()
        {
            return true;
        }

        public override bool GetPauseMenuEnabled()
        {
            return false;
        }
    }
}
