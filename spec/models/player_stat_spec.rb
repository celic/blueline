# == Schema Information
#
# Table name: player_stats
#
#  id              :integer          not null, primary key
#  player_id       :integer
#  team_id         :integer
#  opponent_id     :integer
#  home            :boolean
#  decision        :integer
#  goals           :integer
#  assists         :integer
#  points          :integer
#  plusminus       :integer
#  pim             :integer
#  evg             :integer
#  ppg             :integer
#  shg             :integer
#  gwg             :integer
#  eva             :integer
#  ppa             :integer
#  sha             :integer
#  shots           :integer
#  shot_percentage :decimal(5, 2)
#  shifts          :integer
#  toi             :integer
#
# Indexes
#
#  index_player_stats_on_player_id  (player_id)
#  index_player_stats_on_team_id    (team_id)
#

require 'rails_helper'

RSpec.describe PlayerStat, type: :model do
  pending "add some examples to (or delete) #{__FILE__}"
end
