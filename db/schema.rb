# encoding: UTF-8
# This file is auto-generated from the current state of the database. Instead
# of editing this file, please use the migrations feature of Active Record to
# incrementally modify your database, and then regenerate this schema definition.
#
# Note that this schema.rb definition is the authoritative source for your
# database schema. If you need to create the application database on another
# system, you should be using db:schema:load, not running all the migrations
# from scratch. The latter is a flawed and unsustainable approach (the more migrations
# you'll amass, the slower it'll run and the greater likelihood for issues).
#
# It's strongly recommended that you check this file into your version control system.

ActiveRecord::Schema.define(version: 20160107043001) do

  # These are extensions that must be enabled in order to support this database
  enable_extension "plpgsql"

  create_table "goalie_stats", force: :cascade do |t|
    t.integer "player_id"
    t.integer "team_id"
    t.integer "opponent_id"
    t.boolean "home"
    t.integer "decision"
    t.integer "record"
    t.integer "goals_against"
    t.integer "shots_against"
    t.integer "saves"
    t.decimal "save_percentage", precision: 5, scale: 2
    t.boolean "shutout"
    t.integer "pim"
    t.integer "toi"
  end

  add_index "goalie_stats", ["player_id"], name: "index_goalie_stats_on_player_id", using: :btree
  add_index "goalie_stats", ["team_id"], name: "index_goalie_stats_on_team_id", using: :btree

  create_table "player_stats", force: :cascade do |t|
    t.integer "player_id"
    t.integer "team_id"
    t.integer "opponent_id"
    t.boolean "home"
    t.integer "decision"
    t.integer "goals"
    t.integer "assists"
    t.integer "points"
    t.integer "plusminus"
    t.integer "pim"
    t.integer "evg"
    t.integer "ppg"
    t.integer "shg"
    t.integer "gwg"
    t.integer "eva"
    t.integer "ppa"
    t.integer "sha"
    t.integer "shots"
    t.decimal "shot_percentage", precision: 5, scale: 2
    t.integer "shifts"
    t.integer "toi"
  end

  add_index "player_stats", ["player_id"], name: "index_player_stats_on_player_id", using: :btree
  add_index "player_stats", ["team_id"], name: "index_player_stats_on_team_id", using: :btree

  create_table "players", force: :cascade do |t|
    t.integer "team_id"
    t.string  "name"
    t.integer "position"
    t.string  "key"
  end

  add_index "players", ["key"], name: "index_players_on_key", unique: true, using: :btree
  add_index "players", ["team_id"], name: "index_players_on_team_id", using: :btree

  create_table "teams", force: :cascade do |t|
    t.string "name"
    t.string "abbreviation"
    t.string "city"
    t.string "state"
    t.string "full_name"
  end

  add_index "teams", ["abbreviation"], name: "index_teams_on_abbreviation", using: :btree

end
